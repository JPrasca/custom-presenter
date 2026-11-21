using System.Globalization;
using CustomPresenter.Domain.Songs;
using Microsoft.Data.Sqlite;

namespace CustomPresenter.Infrastructure.Sqlite;

/// <summary>
/// Repositorio SQLite para canciones con índice FTS5, consultas parametrizadas y transacciones ACID.
/// </summary>
public sealed class SqliteSongRepository : ISongRepository
{
    private readonly string _connectionString;

    public SqliteSongRepository(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    public Task CreateAsync(Song song, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(song);
        return CreateBatchAsync([song], cancellationToken);
    }

    public async Task CreateBatchAsync(
        IReadOnlyList<Song> songs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(songs);
        if (songs.Count == 0)
        {
            return;
        }

        await using var connection = await SqliteConnectionFactory
            .OpenAsync(_connectionString, cancellationToken)
            .ConfigureAwait(false);
        await using var tx = (SqliteTransaction)await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var song in songs)
        {
            await InsertSongRecordAsync(connection, tx, song, cancellationToken).ConfigureAwait(false);
            await InsertSlidesAsync(connection, tx, song, cancellationToken).ConfigureAwait(false);
            await InsertFtsRecordAsync(connection, tx, song, cancellationToken).ConfigureAwait(false);
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Song?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await SqliteConnectionFactory
            .OpenAsync(_connectionString, cancellationToken)
            .ConfigureAwait(false);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, title, author, musical_key, raw_lyrics, created_at, updated_at
            FROM songs
            WHERE id = @id;
            """;
        cmd.Parameters.AddWithValue("@id", id.ToString());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var song = ReadSongHeader(reader);
        await reader.CloseAsync().ConfigureAwait(false);

        var slides = await LoadSlidesAsync(connection, song.Id, cancellationToken).ConfigureAwait(false);
        return song with { Slides = slides };
    }

    public async Task<IReadOnlyList<Song>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await SqliteConnectionFactory
            .OpenAsync(_connectionString, cancellationToken)
            .ConfigureAwait(false);

        var headers = new List<Song>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT id, title, author, musical_key, raw_lyrics, created_at, updated_at
                FROM songs
                ORDER BY title COLLATE NOCASE ASC;
                """;

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                headers.Add(ReadSongHeader(reader));
            }
        }

        return await HydrateSlidesAsync(connection, headers, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Song>> SearchAsync(
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

        var headers = new List<Song>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT s.id, s.title, s.author, s.musical_key, s.raw_lyrics, s.created_at, s.updated_at
                FROM songs_fts f
                INNER JOIN songs s ON s.id = f.song_id
                WHERE songs_fts MATCH @ftsQuery
                ORDER BY bm25(songs_fts, 10.0, 2.0, 1.0) ASC
                LIMIT @limit;
                """;
            cmd.Parameters.AddWithValue("@ftsQuery", sanitizedFtsQuery);
            cmd.Parameters.AddWithValue("@limit", Math.Clamp(limit, 1, 200));

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                headers.Add(ReadSongHeader(reader));
            }
        }

        return await HydrateSlidesAsync(connection, headers, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> UpdateAsync(Song song, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(song);

        await using var connection = await SqliteConnectionFactory
            .OpenAsync(_connectionString, cancellationToken)
            .ConfigureAwait(false);
        await using var tx = (SqliteTransaction)await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        int affected;
        await using (var updateCmd = connection.CreateCommand())
        {
            updateCmd.Transaction = tx;
            updateCmd.CommandText = """
                UPDATE songs
                SET title = @title,
                    author = @author,
                    musical_key = @musicalKey,
                    raw_lyrics = @rawLyrics,
                    updated_at = @updatedAt
                WHERE id = @id;
                """;
            updateCmd.Parameters.AddWithValue("@id", song.Id.ToString());
            updateCmd.Parameters.AddWithValue("@title", song.Title);
            updateCmd.Parameters.AddWithValue("@author", (object?)song.Author ?? DBNull.Value);
            updateCmd.Parameters.AddWithValue("@musicalKey", (object?)song.MusicalKey ?? DBNull.Value);
            updateCmd.Parameters.AddWithValue("@rawLyrics", song.RawLyrics);
            updateCmd.Parameters.AddWithValue("@updatedAt", song.UpdatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
            affected = await updateCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        if (affected == 0)
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        await DeleteSlidesAndFtsAsync(connection, tx, song.Id, cancellationToken).ConfigureAwait(false);
        await InsertSlidesAsync(connection, tx, song, cancellationToken).ConfigureAwait(false);
        await InsertFtsRecordAsync(connection, tx, song, cancellationToken).ConfigureAwait(false);

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
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
            deleteFts.CommandText = "DELETE FROM songs_fts WHERE song_id = @id;";
            deleteFts.Parameters.AddWithValue("@id", id.ToString());
            await deleteFts.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        int affected;
        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "DELETE FROM songs WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", id.ToString());
            affected = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return affected > 0;
    }

    private static async Task InsertSongRecordAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Song song,
        CancellationToken cancellationToken)
    {
        await using var insertSong = connection.CreateCommand();
        insertSong.Transaction = transaction;
        insertSong.CommandText = """
            INSERT INTO songs (id, title, author, musical_key, raw_lyrics, created_at, updated_at)
            VALUES (@id, @title, @author, @musicalKey, @rawLyrics, @createdAt, @updatedAt);
            """;
        insertSong.Parameters.AddWithValue("@id", song.Id.ToString());
        insertSong.Parameters.AddWithValue("@title", song.Title);
        insertSong.Parameters.AddWithValue("@author", (object?)song.Author ?? DBNull.Value);
        insertSong.Parameters.AddWithValue("@musicalKey", (object?)song.MusicalKey ?? DBNull.Value);
        insertSong.Parameters.AddWithValue("@rawLyrics", song.RawLyrics);
        insertSong.Parameters.AddWithValue("@createdAt", song.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        insertSong.Parameters.AddWithValue("@updatedAt", song.UpdatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        await insertSong.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertFtsRecordAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Song song,
        CancellationToken cancellationToken)
    {
        await using var insertFts = connection.CreateCommand();
        insertFts.Transaction = transaction;
        insertFts.CommandText = """
            INSERT INTO songs_fts (song_id, title, author, raw_lyrics)
            VALUES (@songId, @title, @author, @rawLyrics);
            """;
        insertFts.Parameters.AddWithValue("@songId", song.Id.ToString());
        insertFts.Parameters.AddWithValue("@title", song.Title);
        insertFts.Parameters.AddWithValue("@author", song.Author ?? string.Empty);
        insertFts.Parameters.AddWithValue("@rawLyrics", song.RawLyrics);
        await insertFts.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task DeleteSlidesAndFtsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid songId,
        CancellationToken cancellationToken)
    {
        await using (var deleteSlides = connection.CreateCommand())
        {
            deleteSlides.Transaction = transaction;
            deleteSlides.CommandText = "DELETE FROM song_slides WHERE song_id = @songId;";
            deleteSlides.Parameters.AddWithValue("@songId", songId.ToString());
            await deleteSlides.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using var deleteFts = connection.CreateCommand();
        deleteFts.Transaction = transaction;
        deleteFts.CommandText = "DELETE FROM songs_fts WHERE song_id = @songId;";
        deleteFts.Parameters.AddWithValue("@songId", songId.ToString());
        await deleteFts.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertSlidesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Song song,
        CancellationToken cancellationToken)
    {
        foreach (var slide in song.Slides)
        {
            await using var insertSlide = connection.CreateCommand();
            insertSlide.Transaction = transaction;
            insertSlide.CommandText = """
                INSERT INTO song_slides (song_id, slide_index, label, slide_text)
                VALUES (@songId, @slideIndex, @label, @slideText);
                """;
            insertSlide.Parameters.AddWithValue("@songId", song.Id.ToString());
            insertSlide.Parameters.AddWithValue("@slideIndex", slide.Index);
            insertSlide.Parameters.AddWithValue("@label", slide.Label);
            insertSlide.Parameters.AddWithValue("@slideText", slide.Text);
            await insertSlide.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<IReadOnlyList<Song>> HydrateSlidesAsync(
        SqliteConnection connection,
        List<Song> headers,
        CancellationToken cancellationToken)
    {
        var result = new List<Song>(headers.Count);
        foreach (var header in headers)
        {
            var slides = await LoadSlidesAsync(connection, header.Id, cancellationToken).ConfigureAwait(false);
            result.Add(header with { Slides = slides });
        }

        return result;
    }

    private static async Task<IReadOnlyList<SongSlide>> LoadSlidesAsync(
        SqliteConnection connection,
        Guid songId,
        CancellationToken cancellationToken)
    {
        var slides = new List<SongSlide>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT slide_index, label, slide_text
            FROM song_slides
            WHERE song_id = @songId
            ORDER BY slide_index ASC;
            """;
        cmd.Parameters.AddWithValue("@songId", songId.ToString());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            slides.Add(new SongSlide(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2)));
        }

        return slides;
    }

    private static Song ReadSongHeader(SqliteDataReader reader)
    {
        var id = Guid.Parse(reader.GetString(0));
        var title = reader.GetString(1);
        var author = reader.IsDBNull(2) ? null : reader.GetString(2);
        var musicalKey = reader.IsDBNull(3) ? null : reader.GetString(3);
        var rawLyrics = reader.GetString(4);
        var createdAt = DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture);
        var updatedAt = DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture);

        return new Song(id, title, author, musicalKey, rawLyrics, [], createdAt, updatedAt);
    }
}
