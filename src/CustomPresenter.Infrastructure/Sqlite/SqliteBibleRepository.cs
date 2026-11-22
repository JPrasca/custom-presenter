using System.Globalization;
using CustomPresenter.Domain.Bibles;
using Microsoft.Data.Sqlite;

namespace CustomPresenter.Infrastructure.Sqlite;

/// <summary>
/// Repositorio SQLite transaccional para versiones y versículos bíblicos con índice FTS5.
/// </summary>
public sealed class SqliteBibleRepository : IBibleRepository
{
    private readonly string _connectionString;

    public SqliteBibleRepository(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    public async Task SaveBibleAsync(
        BibleVersion version,
        IReadOnlyList<BibleVerse> verses,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(verses);

        await using var connection = await SqliteConnectionFactory
            .OpenAsync(_connectionString, cancellationToken)
            .ConfigureAwait(false);
        await using var tx = (SqliteTransaction)await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        await using (var insertVersion = connection.CreateCommand())
        {
            insertVersion.Transaction = tx;
            insertVersion.CommandText = """
                INSERT INTO bible_versions (id, name, abbreviation, imported_at)
                VALUES (@id, @name, @abbr, @importedAt);
                """;
            insertVersion.Parameters.AddWithValue("@id", version.Id.ToString());
            insertVersion.Parameters.AddWithValue("@name", version.Name);
            insertVersion.Parameters.AddWithValue("@abbr", version.Abbreviation);
            insertVersion.Parameters.AddWithValue(
                "@importedAt",
                version.ImportedAtUtc.ToString("O", CultureInfo.InvariantCulture));
            await insertVersion.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await InsertVersesBatchAsync(connection, tx, verses, cancellationToken).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveVersesBatchAsync(
        IReadOnlyList<BibleVerse> verses,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(verses);
        if (verses.Count == 0)
        {
            return;
        }

        await using var connection = await SqliteConnectionFactory
            .OpenAsync(_connectionString, cancellationToken)
            .ConfigureAwait(false);
        await using var tx = (SqliteTransaction)await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        await InsertVersesBatchAsync(connection, tx, verses, cancellationToken).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<BibleVersion>> ListVersionsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await SqliteConnectionFactory
            .OpenAsync(_connectionString, cancellationToken)
            .ConfigureAwait(false);

        var versions = new List<BibleVersion>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, name, abbreviation, imported_at
            FROM bible_versions
            ORDER BY name COLLATE NOCASE ASC;
            """;

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            versions.Add(new BibleVersion(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                reader.GetString(2),
                DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture)));
        }

        return versions;
    }

    public async Task<bool> DeleteVersionAsync(Guid versionId, CancellationToken cancellationToken = default)
    {
        await using var connection = await SqliteConnectionFactory
            .OpenAsync(_connectionString, cancellationToken)
            .ConfigureAwait(false);
        await using var tx = (SqliteTransaction)await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        await using (var deleteFts = connection.CreateCommand())
        {
            deleteFts.Transaction = tx;
            deleteFts.CommandText = "DELETE FROM bible_verses_fts WHERE version_id = @id;";
            deleteFts.Parameters.AddWithValue("@id", versionId.ToString());
            await deleteFts.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        int affected;
        await using (var deleteVersion = connection.CreateCommand())
        {
            deleteVersion.Transaction = tx;
            deleteVersion.CommandText = "DELETE FROM bible_versions WHERE id = @id;";
            deleteVersion.Parameters.AddWithValue("@id", versionId.ToString());
            affected = await deleteVersion.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return affected > 0;
    }

    public async Task<IReadOnlyList<BibleBookSummary>> ListBooksAsync(
        Guid versionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await SqliteConnectionFactory
            .OpenAsync(_connectionString, cancellationToken)
            .ConfigureAwait(false);

        var books = new List<BibleBookSummary>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT book_number, book_name, MAX(chapter)
            FROM bible_verses
            WHERE version_id = @versionId
            GROUP BY book_number, book_name
            ORDER BY book_number ASC;
            """;
        cmd.Parameters.AddWithValue("@versionId", versionId.ToString());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            books.Add(new BibleBookSummary(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetInt32(2)));
        }

        return books;
    }

    public async Task<IReadOnlyList<BibleVerse>> GetChapterVersesAsync(
        Guid versionId,
        int bookNumber,
        int chapter,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await SqliteConnectionFactory
            .OpenAsync(_connectionString, cancellationToken)
            .ConfigureAwait(false);

        var verses = new List<BibleVerse>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT version_id, book_number, book_name, chapter, verse, verse_text
            FROM bible_verses
            WHERE version_id = @versionId AND book_number = @bookNumber AND chapter = @chapter
            ORDER BY verse ASC;
            """;
        cmd.Parameters.AddWithValue("@versionId", versionId.ToString());
        cmd.Parameters.AddWithValue("@bookNumber", bookNumber);
        cmd.Parameters.AddWithValue("@chapter", chapter);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            verses.Add(ReadVerse(reader));
        }

        return verses;
    }

    public async Task<IReadOnlyList<BibleVerse>> FindByReferenceAsync(
        Guid versionId,
        BibleReference reference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);

        var books = await ListBooksAsync(versionId, cancellationToken).ConfigureAwait(false);
        var targetKey = BibleReferenceParser.NormalizeBookKey(reference.BookQuery);

        var matchedByName = books.FirstOrDefault(b =>
            BibleReferenceParser.NormalizeBookKey(b.BookName)
                .StartsWith(targetKey, StringComparison.Ordinal));

        int? resolvedBookNumber = matchedByName?.BookNumber;
        if (resolvedBookNumber is null && reference.CanonicalBookNumber is not null)
        {
            resolvedBookNumber = books
                .FirstOrDefault(b => b.BookNumber == reference.CanonicalBookNumber.Value)
                ?.BookNumber;
        }

        if (resolvedBookNumber is null)
        {
            return [];
        }

        await using var connection = await SqliteConnectionFactory
            .OpenAsync(_connectionString, cancellationToken)
            .ConfigureAwait(false);

        var verses = new List<BibleVerse>();
        await using var cmd = connection.CreateCommand();

        if (reference.StartVerse is null)
        {
            cmd.CommandText = """
                SELECT version_id, book_number, book_name, chapter, verse, verse_text
                FROM bible_verses
                WHERE version_id = @versionId AND book_number = @bookNumber AND chapter = @chapter
                ORDER BY verse ASC;
                """;
        }
        else
        {
            var endVerse = reference.EndVerse ?? reference.StartVerse.Value;
            cmd.CommandText = """
                SELECT version_id, book_number, book_name, chapter, verse, verse_text
                FROM bible_verses
                WHERE version_id = @versionId
                  AND book_number = @bookNumber
                  AND chapter = @chapter
                  AND verse BETWEEN @startVerse AND @endVerse
                ORDER BY verse ASC;
                """;
            cmd.Parameters.AddWithValue("@startVerse", reference.StartVerse.Value);
            cmd.Parameters.AddWithValue("@endVerse", endVerse);
        }

        cmd.Parameters.AddWithValue("@versionId", versionId.ToString());
        cmd.Parameters.AddWithValue("@bookNumber", resolvedBookNumber.Value);
        cmd.Parameters.AddWithValue("@chapter", reference.Chapter);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            verses.Add(ReadVerse(reader));
        }

        return verses;
    }

    public async Task<IReadOnlyList<BibleVerse>> SearchVersesFtsAsync(
        Guid versionId,
        string sanitizedFtsQuery,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sanitizedFtsQuery))
        {
            return [];
        }

        await using var connection = await SqliteConnectionFactory
            .OpenAsync(_connectionString, cancellationToken)
            .ConfigureAwait(false);

        var verses = new List<BibleVerse>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT v.version_id, v.book_number, v.book_name, v.chapter, v.verse, v.verse_text
            FROM bible_verses_fts f
            INNER JOIN bible_verses v
                ON v.version_id = f.version_id
               AND v.book_name = f.book_name
               AND v.chapter = f.chapter
               AND v.verse = f.verse
            WHERE f.version_id = @versionId
              AND bible_verses_fts MATCH @ftsQuery
            ORDER BY bm25(bible_verses_fts, 2.0, 1.0) ASC
            LIMIT @limit;
            """;
        cmd.Parameters.AddWithValue("@versionId", versionId.ToString());
        cmd.Parameters.AddWithValue("@ftsQuery", sanitizedFtsQuery);
        cmd.Parameters.AddWithValue("@limit", Math.Clamp(limit, 1, 200));

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            verses.Add(ReadVerse(reader));
        }

        return verses;
    }

    private static BibleVerse ReadVerse(SqliteDataReader reader) =>
        new(
            Guid.Parse(reader.GetString(0)),
            reader.GetInt32(1),
            reader.GetString(2),
            reader.GetInt32(3),
            reader.GetInt32(4),
            reader.GetString(5));

    private static async Task InsertVersesBatchAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IReadOnlyList<BibleVerse> verses,
        CancellationToken cancellationToken)
    {
        await using var insertVerse = connection.CreateCommand();
        insertVerse.Transaction = transaction;
        insertVerse.CommandText = """
            INSERT OR REPLACE INTO bible_verses (version_id, book_number, book_name, chapter, verse, verse_text)
            VALUES (@versionId, @bookNumber, @bookName, @chapter, @verse, @verseText);
            """;
        var pVersionId = insertVerse.Parameters.Add("@versionId", SqliteType.Text);
        var pBookNumber = insertVerse.Parameters.Add("@bookNumber", SqliteType.Integer);
        var pBookName = insertVerse.Parameters.Add("@bookName", SqliteType.Text);
        var pChapter = insertVerse.Parameters.Add("@chapter", SqliteType.Integer);
        var pVerse = insertVerse.Parameters.Add("@verse", SqliteType.Integer);
        var pVerseText = insertVerse.Parameters.Add("@verseText", SqliteType.Text);

        await using var deleteFts = connection.CreateCommand();
        deleteFts.Transaction = transaction;
        deleteFts.CommandText = """
            DELETE FROM bible_verses_fts
            WHERE version_id = @versionId AND book_name = @bookName AND chapter = @chapter AND verse = @verse;
            """;
        var dVersionId = deleteFts.Parameters.Add("@versionId", SqliteType.Text);
        var dBookName = deleteFts.Parameters.Add("@bookName", SqliteType.Text);
        var dChapter = deleteFts.Parameters.Add("@chapter", SqliteType.Integer);
        var dVerse = deleteFts.Parameters.Add("@verse", SqliteType.Integer);

        await using var insertFts = connection.CreateCommand();
        insertFts.Transaction = transaction;
        insertFts.CommandText = """
            INSERT INTO bible_verses_fts (version_id, book_name, chapter, verse, verse_text)
            VALUES (@versionId, @bookName, @chapter, @verse, @verseText);
            """;
        var fVersionId = insertFts.Parameters.Add("@versionId", SqliteType.Text);
        var fBookName = insertFts.Parameters.Add("@bookName", SqliteType.Text);
        var fChapter = insertFts.Parameters.Add("@chapter", SqliteType.Integer);
        var fVerse = insertFts.Parameters.Add("@verse", SqliteType.Integer);
        var fVerseText = insertFts.Parameters.Add("@verseText", SqliteType.Text);

        foreach (var item in verses)
        {
            var versionIdStr = item.VersionId.ToString();

            pVersionId.Value = versionIdStr;
            pBookNumber.Value = item.BookNumber;
            pBookName.Value = item.BookName;
            pChapter.Value = item.Chapter;
            pVerse.Value = item.Verse;
            pVerseText.Value = item.Text;
            await insertVerse.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            dVersionId.Value = versionIdStr;
            dBookName.Value = item.BookName;
            dChapter.Value = item.Chapter;
            dVerse.Value = item.Verse;
            await deleteFts.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            fVersionId.Value = versionIdStr;
            fBookName.Value = item.BookName;
            fChapter.Value = item.Chapter;
            fVerse.Value = item.Verse;
            fVerseText.Value = item.Text;
            await insertFts.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
