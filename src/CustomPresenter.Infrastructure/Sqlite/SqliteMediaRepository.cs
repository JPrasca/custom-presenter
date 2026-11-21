using System.Globalization;
using CustomPresenter.Domain.Media;
using Microsoft.Data.Sqlite;

namespace CustomPresenter.Infrastructure.Sqlite;

/// <summary>
/// Repositorio SQLite parametrizado para metadatos de activos multimedia.
/// </summary>
public sealed class SqliteMediaRepository : IMediaRepository
{
    private readonly string _connectionString;

    public SqliteMediaRepository(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    public async Task CreateAsync(MediaAsset asset, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(asset);

        await using var connection = await SqliteConnectionFactory
            .OpenAsync(_connectionString, cancellationToken)
            .ConfigureAwait(false);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO media_assets (
                id,
                original_name,
                kind,
                format,
                stored_file_name,
                content_type,
                size_bytes,
                imported_at
            )
            VALUES (
                @id,
                @originalName,
                @kind,
                @format,
                @storedFileName,
                @contentType,
                @sizeBytes,
                @importedAt
            );
            """;
        cmd.Parameters.AddWithValue("@id", asset.Id.ToString("D"));
        cmd.Parameters.AddWithValue("@originalName", asset.OriginalName);
        cmd.Parameters.AddWithValue("@kind", (int)asset.Kind);
        cmd.Parameters.AddWithValue("@format", (int)asset.Format);
        cmd.Parameters.AddWithValue("@storedFileName", asset.StoredFileName);
        cmd.Parameters.AddWithValue("@contentType", asset.ContentType);
        cmd.Parameters.AddWithValue("@sizeBytes", asset.SizeBytes);
        cmd.Parameters.AddWithValue("@importedAt", asset.ImportedAtUtc.ToString("O", CultureInfo.InvariantCulture));

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<MediaAsset?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await SqliteConnectionFactory
            .OpenAsync(_connectionString, cancellationToken)
            .ConfigureAwait(false);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, original_name, kind, format, stored_file_name, content_type, size_bytes, imported_at
            FROM media_assets
            WHERE id = @id;
            """;
        cmd.Parameters.AddWithValue("@id", id.ToString("D"));

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return ReadAsset(reader);
    }

    public async Task<IReadOnlyList<MediaAsset>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await SqliteConnectionFactory
            .OpenAsync(_connectionString, cancellationToken)
            .ConfigureAwait(false);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, original_name, kind, format, stored_file_name, content_type, size_bytes, imported_at
            FROM media_assets
            ORDER BY imported_at DESC, id ASC;
            """;

        var list = new List<MediaAsset>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(ReadAsset(reader));
        }

        return list;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await SqliteConnectionFactory
            .OpenAsync(_connectionString, cancellationToken)
            .ConfigureAwait(false);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM media_assets WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id.ToString("D"));

        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return affected > 0;
    }

    private static MediaAsset ReadAsset(SqliteDataReader reader) =>
        new(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            (MediaKind)reader.GetInt32(2),
            (MediaFormat)reader.GetInt32(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetInt64(6),
            DateTimeOffset.Parse(reader.GetString(7), CultureInfo.InvariantCulture));
}
