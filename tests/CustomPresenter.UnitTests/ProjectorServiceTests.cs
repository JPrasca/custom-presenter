using CustomPresenter.Application.Display;
using CustomPresenter.Application.Live;
using CustomPresenter.Domain.Display;
using CustomPresenter.Domain.Live;
using CustomPresenter.Domain.Ports;
using CustomPresenter.UnitTests.Builders;
using Xunit;

namespace CustomPresenter.UnitTests;

public sealed class ProjectorServiceTests
{
    private sealed class FakeProjectorWindowManager(IReadOnlyList<MonitorInfo> monitors)
        : IProjectorWindowManager
    {
        public bool AllowsWriteIpcHandlers => false;
        public bool IsOpen { get; private set; }
        public MonitorInfo? ActiveMonitor { get; private set; }
        public ProjectorDisplayMode? LastDisplayMode { get; private set; }
        public List<LiveState> SentStates { get; } = [];

        public IReadOnlyList<MonitorInfo> GetMonitors() => monitors;

        public Task OpenAsync(
            MonitorInfo targetMonitor,
            ProjectorDisplayMode displayMode,
            CancellationToken cancellationToken = default)
        {
            IsOpen = true;
            ActiveMonitor = targetMonitor;
            LastDisplayMode = displayMode;
            return Task.CompletedTask;
        }

        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            IsOpen = false;
            ActiveMonitor = null;
            return Task.CompletedTask;
        }

        public ValueTask SendLiveStateAsync(
            LiveState state,
            CancellationToken cancellationToken = default)
        {
            if (IsOpen)
            {
                SentStates.Add(state);
            }

            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public void GetAvailableMonitorsDevuelveListaConNombreResolucionYPosicion()
    {
        // Arrange
        var primary = new MonitorInfoBuilder().Build();
        var secondary = new MonitorInfoBuilder()
            .WithId("mon-2")
            .WithName("Proyector HDMI")
            .WithPosition(1920, 0)
            .WithResolution(1920, 1080)
            .AsSecondary()
            .Build();
        var windowManager = new FakeProjectorWindowManager([primary, secondary]);
        var liveManager = new LiveStateManager([]);
        var service = new ProjectorService(windowManager, liveManager);

        // Act
        var result = service.GetAvailableMonitors();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("Proyector HDMI", result[1].Name);
        Assert.Equal(1920, result[1].X);
        Assert.Equal(1920, result[1].Width);
    }

    [Fact]
    public async Task OpenOnMonitorAsyncConUnSoloMonitorUsaModoVentanaParaNoBloquearPantalla()
    {
        // Arrange
        var onlyMonitor = new MonitorInfoBuilder().Build();
        var windowManager = new FakeProjectorWindowManager([onlyMonitor]);
        var liveManager = new LiveStateManager([]);
        var slide = new SlideContentBuilder().WithText("Salmo 100").Build();
        await liveManager.ProjectSlideAsync(slide);
        var service = new ProjectorService(windowManager, liveManager);

        // Act
        await service.OpenOnMonitorAsync(onlyMonitor.Id);

        // Assert
        Assert.True(windowManager.IsOpen);
        Assert.Equal(ProjectorDisplayMode.Windowed, windowManager.LastDisplayMode);
        Assert.Single(windowManager.SentStates);
        Assert.Equal(slide, windowManager.SentStates[0].CurrentSlide);
    }

    [Fact]
    public async Task OpenOnMonitorAsyncConDosMonitoresUsaFullscreenSinBordesSalvoModoVentanaExplicito()
    {
        // Arrange
        var mon1 = new MonitorInfoBuilder().Build();
        var mon2 = new MonitorInfoBuilder().WithId("mon-2").WithPosition(1920, 0).AsSecondary().Build();
        var windowManager = new FakeProjectorWindowManager([mon1, mon2]);
        var liveManager = new LiveStateManager([]);
        var service = new ProjectorService(windowManager, liveManager);

        // Act
        await service.OpenOnMonitorAsync("mon-2");
        var modeDefault = windowManager.LastDisplayMode;
        await service.OpenOnMonitorAsync("mon-2", forceWindowed: true);
        var modeForcedWindowed = windowManager.LastDisplayMode;

        // Assert
        Assert.Equal(ProjectorDisplayMode.BorderlessFullscreen, modeDefault);
        Assert.Equal(ProjectorDisplayMode.Windowed, modeForcedWindowed);
    }

    [Fact]
    public async Task OpenOnMonitorAsyncConMonitorInexistenteLanzaDomainException()
    {
        // Arrange
        var mon1 = new MonitorInfoBuilder().Build();
        var windowManager = new FakeProjectorWindowManager([mon1]);
        var liveManager = new LiveStateManager([]);
        var service = new ProjectorService(windowManager, liveManager);

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(
            () => service.OpenOnMonitorAsync("inexistente"));
    }

    [Fact]
    public async Task CloseProjectorAsyncCierraVentanaYLimpiaMonitorActivo()
    {
        // Arrange
        var mon1 = new MonitorInfoBuilder().Build();
        var windowManager = new FakeProjectorWindowManager([mon1]);
        var liveManager = new LiveStateManager([]);
        var service = new ProjectorService(windowManager, liveManager);
        await service.OpenOnMonitorAsync(mon1.Id);

        // Act
        await service.CloseProjectorAsync();

        // Assert
        Assert.False(windowManager.IsOpen);
        Assert.Null(windowManager.ActiveMonitor);
    }
}
