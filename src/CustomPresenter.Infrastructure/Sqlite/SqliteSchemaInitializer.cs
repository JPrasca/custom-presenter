namespace CustomPresenter.Infrastructure.Sqlite;

/// <summary>
/// Inicializador del esquema relacional SQLite e índices FTS5 para canciones y biblias.
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

            CREATE VIRTUAL TABLE IF NOT EXISTS songs_fts USING fts5(
                song_id UNINDEXED,
                title,
                author,
                raw_lyrics,
                tokenize='unicode61 remove_diacritics 2'
            );

            INSERT INTO songs_fts(song_id, title, author, raw_lyrics)
            SELECT id, title, COALESCE(author, ''), raw_lyrics
            FROM songs
            WHERE id NOT IN (SELECT song_id FROM songs_fts);

            CREATE TABLE IF NOT EXISTS bible_versions (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                abbreviation TEXT NOT NULL,
                imported_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS bible_verses (
                version_id TEXT NOT NULL,
                book_number INTEGER NOT NULL,
                book_name TEXT NOT NULL,
                chapter INTEGER NOT NULL,
                verse INTEGER NOT NULL,
                verse_text TEXT NOT NULL,
                PRIMARY KEY (version_id, book_number, chapter, verse),
                FOREIGN KEY (version_id) REFERENCES bible_versions(id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS idx_bible_verses_lookup
                ON bible_verses(version_id, book_name, chapter, verse);

            CREATE VIRTUAL TABLE IF NOT EXISTS bible_verses_fts USING fts5(
                version_id UNINDEXED,
                book_name,
                chapter UNINDEXED,
                verse UNINDEXED,
                verse_text,
                tokenize='unicode61 remove_diacritics 2'
            );

            CREATE TABLE IF NOT EXISTS announcements (
                id TEXT PRIMARY KEY,
                title TEXT NOT NULL,
                raw_body TEXT NOT NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS announcement_slides (
                announcement_id TEXT NOT NULL,
                slide_index INTEGER NOT NULL,
                slide_text TEXT NOT NULL,
                PRIMARY KEY (announcement_id, slide_index),
                FOREIGN KEY (announcement_id) REFERENCES announcements(id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS idx_announcements_title ON announcements(title);
            """;

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
