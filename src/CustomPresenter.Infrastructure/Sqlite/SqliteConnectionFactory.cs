using Microsoft.Data.Sqlite;

namespace CustomPresenter.Infrastructure.Sqlite;

/// <summary>
/// Fábrica segura de conexiones SQLite con validación de ruta y PRAGMAs de concurrencia e integridad.
/// </summary>
public static class SqliteConnectionFactory
{
    /// <summary>
    /// Construye una cadena de conexión validando la ruta con Path.GetFullPath para prevenir Path Traversal.
    /// </summary>
    public static string BuildConnectionString(
        string databasePath,
        string? allowedRootDirectory = null,
        bool pooling = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var fullDbPath = Path.GetFullPath(databasePath);

        if (!string.IsNullOrWhiteSpace(allowedRootDirectory))
        {
            var fullRoot = Path.GetFullPath(allowedRootDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            if (!fullDbPath.StartsWith(fullRoot, StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException(
                    "La ruta de la base de datos excede el directorio permitido.");
            }
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = fullDbPath,
            Pooling = pooling,
        };

        return builder.ToString();
    }

    /// <summary>
    /// Abre una conexión SQLite validando la ruta y activando WAL y claves foráneas.
    /// </summary>
    public static async Task<SqliteConnection> OpenAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var csBuilder = new SqliteConnectionStringBuilder(connectionString);
        if (!string.Equals(csBuilder.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(csBuilder.DataSource))
        {
            csBuilder.DataSource = Path.GetFullPath(csBuilder.DataSource);
        }

        var connection = new SqliteConnection(csBuilder.ToString());
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var pragmaCmd = connection.CreateCommand();
        pragmaCmd.CommandText = """
            PRAGMA journal_mode = WAL;
            PRAGMA foreign_keys = ON;
            """;
        await pragmaCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        return connection;
    }
}
