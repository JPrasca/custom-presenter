using System.Globalization;
using CustomPresenter.Domain.Songs;
using Microsoft.Data.Sqlite;

namespace CustomPresenter.Infrastructure.Sqlite;

/// <summary>
/// Repositorio SQLite para canciones con consultas 100 % parametrizadas y transacciones ACID.
/// </summary>
public sealed class SqliteSongRepository : ISongRepository
{
    private readonly string _connectionString;

    public SqliteSongRepository(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    public async Task CreateAsync(Song song, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(song);

        await using var connection = await SqliteConnectionFactory
            .OpenAsync(_connectionString, cancellationToken)
            .ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using (var insertSong = connection.CreateCommand())
        {
            insertSong.Transaction = (SqliteTransaction)tx;
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

        await InsertSlidesAsync(connection, (SqliteTransaction)tx, song, cancellationToken)
            .ConfigureAwait(false);

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

        var result = new List<Song>(headers.Count);
        foreach (var header in headers)
        {
            var slides = await LoadSlidesAsync(connection, header.Id, cancellationToken).ConfigureAwait(false);
            result.Add(header with { Slides = slides });
        }

        return result;
    }

    public async Task<bool> UpdateAsync(Song song, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(song);

        await using var connection = await SqliteConnectionFactory
            .OpenAsync(_connectionString, cancellationToken)
            .ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        int affected;
        await using (var updateCmd = connection.CreateCommand())
        {
            updateCmd.Transaction = (SqliteTransaction)tx;
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

        await using (var deleteSlides = connection.CreateCommand())
        {
            deleteSlides.Transaction = (SqliteTransaction)tx;
            deleteSlides.CommandText = "DELETE FROM song_slides WHERE song_id = @songId;";
            deleteSlides.Parameters.AddWithValue("@songId", song.Id.ToString());
            await deleteSlides.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await InsertSlidesAsync(connection, (SqliteTransaction)tx, song, cancellationToken)
            .ConfigureAwait(false);

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await SqliteConnectionFactory
            .OpenAsync(_connectionString, cancellationToken)
            .ConfigureAwait(false);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM songs WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id.ToString());

        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return affected > 0;
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
