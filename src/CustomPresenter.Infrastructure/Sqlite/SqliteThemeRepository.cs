using System.Globalization;
using System.Text.Json;
using CustomPresenter.Domain.Live;
using CustomPresenter.Domain.Themes;
using Microsoft.Data.Sqlite;

namespace CustomPresenter.Infrastructure.Sqlite;

/// <summary>
/// Repositorio SQLite para temas independientes con validación de dominio y fallback seguro.
/// </summary>
public sealed class SqliteThemeRepository : IThemeRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        MaxDepth = 8,
    };

    private readonly string _connectionString;

    public SqliteThemeRepository(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    public async Task<ThemeSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await SqliteConnectionFactory
            .OpenAsync(_connectionString, cancellationToken)
            .ConfigureAwait(false);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT target, config_json
            FROM themes;
            """;

        var projector = ProjectorTheme.Default;
        var obs = ObsTheme.Default;

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var target = (ThemeTarget)reader.GetInt32(0);
            var json = reader.GetString(1);

            if (target == ThemeTarget.Projector)
            {
                projector = DeserializeProjectorOrDefault(json);
            }
            else if (target == ThemeTarget.ObsLowerThird)
            {
                obs = DeserializeObsOrDefault(json);
            }
        }

        return new ThemeSettings(projector, obs);
    }

    public async Task SaveAsync(ThemeSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var validatedProjector = ProjectorTheme.Create(
            settings.Projector.FontFamily,
            settings.Projector.FontSizePx,
            settings.Projector.TextColor,
            settings.Projector.BackgroundColor,
            settings.Projector.Alignment);

        var validatedObs = ObsTheme.Create(
            settings.Obs.FontFamily,
            settings.Obs.FontSizePx,
            settings.Obs.TextColor,
            settings.Obs.BoxBackgroundColor,
            settings.Obs.Position,
            settings.Obs.MarginHorizontalPx,
            settings.Obs.MarginVerticalPx,
            settings.Obs.BorderRadiusPx);

        await using var connection = await SqliteConnectionFactory
            .OpenAsync(_connectionString, cancellationToken)
            .ConfigureAwait(false);

        await using var tx = (SqliteTransaction)await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);

        await UpsertTargetAsync(
            connection,
            tx,
            ThemeTarget.Projector,
            JsonSerializer.Serialize(validatedProjector, JsonOptions),
            now,
            cancellationToken).ConfigureAwait(false);

        await UpsertTargetAsync(
            connection,
            tx,
            ThemeTarget.ObsLowerThird,
            JsonSerializer.Serialize(validatedObs, JsonOptions),
            now,
            cancellationToken).ConfigureAwait(false);

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task UpsertTargetAsync(
        SqliteConnection connection,
        SqliteTransaction tx,
        ThemeTarget target,
        string json,
        string updatedAt,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO themes (target, config_json, updated_at)
            VALUES (@target, @configJson, @updatedAt)
            ON CONFLICT(target) DO UPDATE SET
                config_json = excluded.config_json,
                updated_at = excluded.updated_at;
            """;
        cmd.Parameters.AddWithValue("@target", (int)target);
        cmd.Parameters.AddWithValue("@configJson", json);
        cmd.Parameters.AddWithValue("@updatedAt", updatedAt);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static ProjectorTheme DeserializeProjectorOrDefault(string json)
    {
        try
        {
            var raw = JsonSerializer.Deserialize<ProjectorTheme>(json, JsonOptions);
            return raw is null
                ? ProjectorTheme.Default
                : ProjectorTheme.Create(
                    raw.FontFamily,
                    raw.FontSizePx,
                    raw.TextColor,
                    raw.BackgroundColor,
                    raw.Alignment);
        }
        catch (Exception ex) when (ex is JsonException or DomainException or ArgumentException)
        {
            return ProjectorTheme.Default;
        }
    }

    private static ObsTheme DeserializeObsOrDefault(string json)
    {
        try
        {
            var raw = JsonSerializer.Deserialize<ObsTheme>(json, JsonOptions);
            return raw is null
                ? ObsTheme.Default
                : ObsTheme.Create(
                    raw.FontFamily,
                    raw.FontSizePx,
                    raw.TextColor,
                    raw.BoxBackgroundColor,
                    raw.Position,
                    raw.MarginHorizontalPx,
                    raw.MarginVerticalPx,
                    raw.BorderRadiusPx);
        }
        catch (Exception ex) when (ex is JsonException or DomainException or ArgumentException)
        {
            return ObsTheme.Default;
        }
    }
}
