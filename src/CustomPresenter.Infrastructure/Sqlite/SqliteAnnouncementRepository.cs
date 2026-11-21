using System.Globalization;
using CustomPresenter.Domain.Announcements;
using Microsoft.Data.Sqlite;

namespace CustomPresenter.Infrastructure.Sqlite;

/// <summary>
/// Repositorio SQLite para anuncios y textos predefinidos con consultas parametrizadas.
/// </summary>
public sealed class SqliteAnnouncementRepository : IAnnouncementRepository
{
    private readonly string _connectionString;

    public SqliteAnnouncementRepository(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    public async Task CreateAsync(Announcement announcement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(announcement);

        await using var connection = await SqliteConnectionFactory
            .OpenAsync(_connectionString, cancellationToken)
            .ConfigureAwait(false);
        await using var tx = (SqliteTransaction)await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        await using (var insertCmd = connection.CreateCommand())
        {
            insertCmd.Transaction = tx;
            insertCmd.CommandText = """
                INSERT INTO announcements (id, title, raw_body, created_at, updated_at)
                VALUES (@id, @title, @rawBody, @createdAt, @updatedAt);
                """;
            insertCmd.Parameters.AddWithValue("@id", announcement.Id.ToString());
            insertCmd.Parameters.AddWithValue("@title", announcement.Title);
            insertCmd.Parameters.AddWithValue("@rawBody", announcement.RawBody);
            insertCmd.Parameters.AddWithValue("@createdAt", announcement.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
            insertCmd.Parameters.AddWithValue("@updatedAt", announcement.UpdatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
            await insertCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await InsertSlidesAsync(connection, tx, announcement, cancellationToken).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Announcement?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await SqliteConnectionFactory
            .OpenAsync(_connectionString, cancellationToken)
            .ConfigureAwait(false);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, title, raw_body, created_at, updated_at
            FROM announcements
            WHERE id = @id;
            """;
        cmd.Parameters.AddWithValue("@id", id.ToString());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var header = ReadHeader(reader);
        await reader.CloseAsync().ConfigureAwait(false);

        var slides = await LoadSlidesAsync(connection, header.Id, cancellationToken).ConfigureAwait(false);
        return header with { Slides = slides };
    }

    public async Task<IReadOnlyList<Announcement>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await SqliteConnectionFactory
            .OpenAsync(_connectionString, cancellationToken)
            .ConfigureAwait(false);

        var headers = new List<Announcement>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT id, title, raw_body, created_at, updated_at
                FROM announcements
                ORDER BY title COLLATE NOCASE ASC;
                """;

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                headers.Add(ReadHeader(reader));
            }
        }

        var result = new List<Announcement>(headers.Count);
        foreach (var header in headers)
        {
            var slides = await LoadSlidesAsync(connection, header.Id, cancellationToken).ConfigureAwait(false);
            result.Add(header with { Slides = slides });
        }

        return result;
    }

    public async Task<bool> UpdateAsync(Announcement announcement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(announcement);

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
                UPDATE announcements
                SET title = @title,
                    raw_body = @rawBody,
                    updated_at = @updatedAt
                WHERE id = @id;
                """;
            updateCmd.Parameters.AddWithValue("@id", announcement.Id.ToString());
            updateCmd.Parameters.AddWithValue("@title", announcement.Title);
            updateCmd.Parameters.AddWithValue("@rawBody", announcement.RawBody);
            updateCmd.Parameters.AddWithValue("@updatedAt", announcement.UpdatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
            affected = await updateCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        if (affected == 0)
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        await using (var deleteSlides = connection.CreateCommand())
        {
            deleteSlides.Transaction = tx;
            deleteSlides.CommandText = "DELETE FROM announcement_slides WHERE announcement_id = @id;";
            deleteSlides.Parameters.AddWithValue("@id", announcement.Id.ToString());
            await deleteSlides.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await InsertSlidesAsync(connection, tx, announcement, cancellationToken).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await SqliteConnectionFactory
            .OpenAsync(_connectionString, cancellationToken)
            .ConfigureAwait(false);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM announcements WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id.ToString());

        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return affected > 0;
    }

    private static async Task InsertSlidesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Announcement announcement,
        CancellationToken cancellationToken)
    {
        foreach (var slide in announcement.Slides)
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT INTO announcement_slides (announcement_id, slide_index, slide_text)
                VALUES (@id, @index, @text);
                """;
            cmd.Parameters.AddWithValue("@id", announcement.Id.ToString());
            cmd.Parameters.AddWithValue("@index", slide.Index);
            cmd.Parameters.AddWithValue("@text", slide.Text);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<IReadOnlyList<AnnouncementSlide>> LoadSlidesAsync(
        SqliteConnection connection,
        Guid announcementId,
        CancellationToken cancellationToken)
    {
        var slides = new List<AnnouncementSlide>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT slide_index, slide_text
            FROM announcement_slides
            WHERE announcement_id = @id
            ORDER BY slide_index ASC;
            """;
        cmd.Parameters.AddWithValue("@id", announcementId.ToString());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            slides.Add(new AnnouncementSlide(reader.GetInt32(0), reader.GetString(1)));
        }

        return slides;
    }

    private static Announcement ReadHeader(SqliteDataReader reader) =>
        new(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.GetString(2),
            [],
            DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture),
            DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture));
}
