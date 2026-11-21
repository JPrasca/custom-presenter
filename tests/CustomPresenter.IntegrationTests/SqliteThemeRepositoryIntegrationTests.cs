using CustomPresenter.Application.Live;
using CustomPresenter.Application.Themes;
using CustomPresenter.Domain.Themes;
using CustomPresenter.Infrastructure.Sqlite;
using Xunit;

namespace CustomPresenter.IntegrationTests;

public sealed class SqliteThemeRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(),
        $"custom-presenter-theme-{Guid.NewGuid():N}.db");

    private string ConnectionString => $"Data Source={_dbPath};Pooling=False";

    public async Task InitializeAsync()
    {
        await SqliteSchemaInitializer.EnsureCreatedAsync(ConnectionString);
    }

    public Task DisposeAsync()
    {
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task GuardarYRecargarTemasEnSqlitePersisteYPropagaEnLiveState()
    {
        // Arrange
        var repo = new SqliteThemeRepository(ConnectionString);
        var service = new ThemeService(repo);
        var broadcaster = new ChannelLiveBroadcaster();
        var liveManager = new LiveStateManager([broadcaster]);
        using var reader = broadcaster.Subscribe();

        var projector = ProjectorTheme.Create("Inter", 76, "#FDE68A", "#111827", TextAlignment.Left);
        var obs = ObsTheme.Create("Roboto", 40, "#FFFFFF", "#0F172ACC", LowerThirdPosition.Top, 72, 36, 14);

        // Act
        await service.UpdateProjectorThemeAsync(projector, liveManager);
        await service.UpdateObsThemeAsync(obs, liveManager);
        var reloaded = await repo.GetAsync();

        // Assert
        Assert.Equal(76, reloaded.Projector.FontSizePx);
        Assert.Equal("#FDE68A", reloaded.Projector.TextColor);
        Assert.Equal(TextAlignment.Left, reloaded.Projector.Alignment);
        Assert.Equal(LowerThirdPosition.Top, reloaded.Obs.Position);
        Assert.Equal(40, reloaded.Obs.FontSizePx);
        Assert.Equal(40, liveManager.Snapshot().EffectiveTheme.Obs.FontSizePx);
    }

    [Fact]
    public async Task RegistroCorruptoEnSqliteHaceFallbackATemaPorDefecto()
    {
        // Arrange
        await using (var conn = await SqliteConnectionFactory.OpenAsync(ConnectionString))
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO themes (target, config_json, updated_at)
                VALUES (@target, @json, @updatedAt);
                """;
            cmd.Parameters.AddWithValue("@target", (int)ThemeTarget.Projector);
            cmd.Parameters.AddWithValue("@json", "{\"fontSizePx\": 99999, \"textColor\": \"not-hex\"}");
            cmd.Parameters.AddWithValue("@updatedAt", DateTimeOffset.UtcNow.ToString("O"));
            await cmd.ExecuteNonQueryAsync();
        }

        var repo = new SqliteThemeRepository(ConnectionString);

        // Act
        var loaded = await repo.GetAsync();

        // Assert
        Assert.Equal(ThemeSettings.Default.Projector, loaded.Projector);
        Assert.Equal(ThemeSettings.Default.Obs, loaded.Obs);
    }
}
