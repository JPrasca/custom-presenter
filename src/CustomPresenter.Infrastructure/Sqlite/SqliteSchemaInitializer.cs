namespace CustomPresenter.Infrastructure.Sqlite;

/// <summary>
/// Inicializador del esquema relacional SQLite para canciones y diapositivas.
/// </summary>
public static class SqliteSchemaInitializer
{
    public static async Task EnsureCreatedAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await SqliteConnectionFactory
            .OpenAsync(connectionString, cancellationToken)
            .ConfigureAwait(false);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS songs (
                id TEXT PRIMARY KEY,
                title TEXT NOT NULL,
                author TEXT NULL,
                musical_key TEXT NULL,
                raw_lyrics TEXT NOT NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS song_slides (
                song_id TEXT NOT NULL,
                slide_index INTEGER NOT NULL,
                label TEXT NOT NULL,
                slide_text TEXT NOT NULL,
                PRIMARY KEY (song_id, slide_index),
                FOREIGN KEY (song_id) REFERENCES songs(id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS idx_songs_title ON songs(title);
            """;

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
