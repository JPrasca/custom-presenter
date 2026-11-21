using System.Globalization;
using CustomPresenter.Application.Announcements;
using CustomPresenter.Application.Live;
using CustomPresenter.Domain.Announcements;
using CustomPresenter.Infrastructure.Sqlite;
using Xunit;

namespace CustomPresenter.IntegrationTests;

public sealed class SqliteAnnouncementRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(),
        $"custom-presenter-ann-{Guid.NewGuid():N}.db");

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
    public async Task CrudCompletoEnSqlitePersisteProyectaYEliminaPorCascada()
    {
        // Arrange
        var repo = new SqliteAnnouncementRepository(ConnectionString);
        var service = new AnnouncementService(repo);
        var broadcaster = new ChannelLiveBroadcaster();
        var liveManager = new LiveStateManager([broadcaster]);
        using var subscription = broadcaster.Subscribe();

        // Act - Create & Project
        var created = await service.CreateAsync("Avisos", "Aviso 1\n\nAviso 2");
        await service.ProjectSlideAsync(created.Id, 0, liveManager);
        var projectedState = await subscription.Reader.ReadAsync();

        // Act - Update
        var updated = await service.UpdateAsync(created.Id, "Avisos Editados", "Único aviso");
        var loadedAfterUpdate = await service.GetByIdAsync(created.Id);

        // Act - Delete
        var deleted = await service.DeleteAsync(created.Id);
        var remainingSlides = await CountSlidesAsync(created.Id);

        // Assert
        Assert.Equal("Aviso 1", projectedState.CurrentSlide?.Text);
        Assert.Equal("Avisos Editados", updated.Title);
        Assert.NotNull(loadedAfterUpdate);
        Assert.Single(loadedAfterUpdate.Slides);
        Assert.True(deleted);
        Assert.Equal(0, remainingSlides);
    }

    [Fact]
    public async Task CreateAsyncConPayloadSqlInjectionLoAlmacenaComoTextoLiteral()
    {
        // Arrange
        var repo = new SqliteAnnouncementRepository(ConnectionString);
        var service = new AnnouncementService(repo);
        const string sqliTitle = "'); DROP TABLE announcements; --";

        // Act
        await service.CreateAsync(sqliTitle, "Texto seguro");
        var all = await service.ListAllAsync();

        // Assert
        Assert.Single(all);
        Assert.Equal(sqliTitle, all[0].Title);
    }

    private async Task<long> CountSlidesAsync(Guid announcementId)
    {
        await using var conn = await SqliteConnectionFactory.OpenAsync(ConnectionString);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM announcement_slides WHERE announcement_id = @id;";
        cmd.Parameters.AddWithValue("@id", announcementId.ToString());
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }
}
