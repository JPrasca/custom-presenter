using System.Globalization;
using CustomPresenter.Domain.Playlists;
using Microsoft.Data.Sqlite;

namespace CustomPresenter.Infrastructure.Sqlite;

/// <summary>
/// Repositorio SQLite transaccional y determinista para listas de servicio mixtas.
/// </summary>
public sealed class SqlitePlaylistRepository : IPlaylistRepository
{
    private readonly string _connectionString;

    public SqlitePlaylistRepository(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    public async Task CreateAsync(Playlist playlist, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(playlist);

        await using var connection = await SqliteConnectionFactory
            .OpenAsync(_connectionString, cancellationToken)
            .ConfigureAwait(false);

        await using var tx = (SqliteTransaction)await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO playlists (id, name, created_at, updated_at)
                VALUES (@id, @name, @createdAt, @updatedAt);
                """;
            cmd.Parameters.AddWithValue("@id", playlist.Id.ToString("D"));
            cmd.Parameters.AddWithValue("@name", playlist.Name);
            cmd.Parameters.AddWithValue("@createdAt", playlist.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("@updatedAt", playlist.UpdatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await InsertItemsAsync(connection, tx, playlist.Id, playlist.Items, cancellationToken)
            .ConfigureAwait(false);

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Playlist?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await SqliteConnectionFactory
            .OpenAsync(_connectionString, cancellationToken)
            .ConfigureAwait(false);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, name, created_at, updated_at
            FROM playlists
            WHERE id = @id;
            """;
        cmd.Parameters.AddWithValue("@id", id.ToString("D"));

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var playlistId = Guid.Parse(reader.GetString(0));
        var name = reader.GetString(1);
        var createdAt = DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture);
        var updatedAt = DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture);
        await reader.CloseAsync().ConfigureAwait(false);

        var items = await LoadItemsAsync(connection, playlistId, cancellationToken).ConfigureAwait(false);
        return new Playlist(playlistId, name, items, createdAt, updatedAt);
    }

    public async Task<IReadOnlyList<Playlist>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await SqliteConnectionFactory
            .OpenAsync(_connectionString, cancellationToken)
            .ConfigureAwait(false);

        var itemsByPlaylist = await LoadAllItemsGroupedAsync(connection, cancellationToken).ConfigureAwait(false);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, name, created_at, updated_at
            FROM playlists
            ORDER BY name COLLATE NOCASE ASC, id ASC;
            """;

        var result = new List<Playlist>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var id = Guid.Parse(reader.GetString(0));
            var items = itemsByPlaylist.TryGetValue(id, out var list)
                ? list
                : (IReadOnlyList<PlaylistItem>)[];

            result.Add(new Playlist(
                id,
                reader.GetString(1),
                items,
                DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture),
                DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture)));
        }

        return result;
    }

    public async Task<bool> UpdateAsync(Playlist playlist, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(playlist);

        await using var connection = await SqliteConnectionFactory
            .OpenAsync(_connectionString, cancellationToken)
            .ConfigureAwait(false);

        await using var tx = (SqliteTransaction)await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        int affected;
        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                UPDATE playlists
                SET name = @name,
                    updated_at = @updatedAt
                WHERE id = @id;
                """;
            cmd.Parameters.AddWithValue("@id", playlist.Id.ToString("D"));
            cmd.Parameters.AddWithValue("@name", playlist.Name);
            cmd.Parameters.AddWithValue("@updatedAt", playlist.UpdatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
            affected = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        if (affected == 0)
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        await using (var deleteItemsCmd = connection.CreateCommand())
        {
            deleteItemsCmd.Transaction = tx;
            deleteItemsCmd.CommandText = "DELETE FROM playlist_items WHERE playlist_id = @playlistId;";
            deleteItemsCmd.Parameters.AddWithValue("@playlistId", playlist.Id.ToString("D"));
            await deleteItemsCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await InsertItemsAsync(connection, tx, playlist.Id, playlist.Items, cancellationToken)
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
        cmd.CommandText = "DELETE FROM playlists WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id.ToString("D"));

        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return affected > 0;
    }

    private static async Task InsertItemsAsync(
        SqliteConnection connection,
        SqliteTransaction tx,
        Guid playlistId,
        IReadOnlyList<PlaylistItem> items,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO playlist_items (
                    id,
                    playlist_id,
                    position,
                    item_kind,
                    reference_key,
                    display_title,
                    subtitle
                )
                VALUES (
                    @id,
                    @playlistId,
                    @position,
                    @itemKind,
                    @referenceKey,
                    @displayTitle,
                    @subtitle
                );
                """;
            cmd.Parameters.AddWithValue("@id", item.Id.ToString("D"));
            cmd.Parameters.AddWithValue("@playlistId", playlistId.ToString("D"));
            cmd.Parameters.AddWithValue("@position", i);
            cmd.Parameters.AddWithValue("@itemKind", (int)item.Kind);
            cmd.Parameters.AddWithValue("@referenceKey", item.ReferenceKey);
            cmd.Parameters.AddWithValue("@displayTitle", item.DisplayTitle);
            cmd.Parameters.AddWithValue("@subtitle", (object?)item.Subtitle ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<List<PlaylistItem>> LoadItemsAsync(
        SqliteConnection connection,
        Guid playlistId,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, position, item_kind, reference_key, display_title, subtitle
            FROM playlist_items
            WHERE playlist_id = @playlistId
            ORDER BY position ASC, id ASC;
            """;
        cmd.Parameters.AddWithValue("@playlistId", playlistId.ToString("D"));

        var items = new List<PlaylistItem>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            items.Add(new PlaylistItem(
                Guid.Parse(reader.GetString(0)),
                reader.GetInt32(1),
                (PlaylistItemKind)reader.GetInt32(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5)));
        }

        return items;
    }

    private static async Task<Dictionary<Guid, List<PlaylistItem>>> LoadAllItemsGroupedAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT playlist_id, id, position, item_kind, reference_key, display_title, subtitle
            FROM playlist_items
            ORDER BY playlist_id ASC, position ASC, id ASC;
            """;

        var map = new Dictionary<Guid, List<PlaylistItem>>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var playlistId = Guid.Parse(reader.GetString(0));
            if (!map.TryGetValue(playlistId, out var list))
            {
                list = [];
                map[playlistId] = list;
            }

            list.Add(new PlaylistItem(
                Guid.Parse(reader.GetString(1)),
                reader.GetInt32(2),
                (PlaylistItemKind)reader.GetInt32(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6)));
        }

        return map;
    }
}
