using CustomPresenter.Application.Display;
using CustomPresenter.Application.Live;
using CustomPresenter.Domain.Display;
using CustomPresenter.Domain.Live;
using CustomPresenter.IntegrationTests.Builders;
using Xunit;

namespace CustomPresenter.IntegrationTests;

public sealed class ProjectorLifecycleIntegrationTests
{
    private sealed class InMemoryProjectorWindowManager : IProjectorWindowManager
    {
        private readonly List<MonitorInfo> _monitors =
        [
            new("mon-1", "Pantalla Integrada", 0, 0, 1920, 1080, true),
            new("mon-2", "Proyector Salón", 1920, 0, 1920, 1080, false),
        ];

        public bool AllowsWriteIpcHandlers => false;
        public bool IsOpen { get; private set; }
        public MonitorInfo? ActiveMonitor { get; private set; }
        public List<LiveState> ReceivedStates { get; } = [];

        public IReadOnlyList<MonitorInfo> GetMonitors() => _monitors;

        public Task OpenAsync(
            MonitorInfo targetMonitor,
            ProjectorDisplayMode displayMode,
            CancellationToken cancellationToken = default)
        {
            IsOpen = true;
            ActiveMonitor = targetMonitor;
            return Task.CompletedTask;
        }

        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            SimulateExternalClose();
            return Task.CompletedTask;
        }

        public void SimulateExternalClose()
        {
            IsOpen = false;
            ActiveMonitor = null;
        }

        public ValueTask SendLiveStateAsync(
            LiveState state,
            CancellationToken cancellationToken = default)
        {
            if (!IsOpen)
            {
                return ValueTask.CompletedTask;
            }

            ReceivedStates.Add(state);
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task CicloDeVidaProyectorRecibeEstadosEnTiempoRealYSeDetieneAlCerrar()
    {
        // Arrange
        var windowManager = new InMemoryProjectorWindowManager();
        var broadcaster = new ChannelLiveBroadcaster();
        var liveManager = new LiveStateManager([broadcaster]);
        using var projectorService = new ProjectorService(windowManager, liveManager, broadcaster);
        var slide = new SlideContentBuilder().WithText("Cuan grande es Él").Build();

        // Act
        await projectorService.OpenOnMonitorAsync("mon-2");
        await liveManager.ProjectSlideAsync(slide);
        await liveManager.ToggleClearTextAsync();
        await liveManager.ToggleBlackoutAsync();

        // Esperar breve propagación del canal en segundo plano
        await WaitUntilAsync(() => windowManager.ReceivedStates.Count >= 4);
        await projectorService.CloseProjectorAsync();
        await liveManager.ToggleBlackoutAsync();
        await Task.Delay(30);

        // Assert
        Assert.False(windowManager.AllowsWriteIpcHandlers);
        Assert.False(windowManager.IsOpen);
        Assert.Equal(4, windowManager.ReceivedStates.Count);
        Assert.Equal(EffectiveDisplayMode.Slide, windowManager.ReceivedStates[1].EffectiveMode);
        Assert.Equal(EffectiveDisplayMode.ClearText, windowManager.ReceivedStates[2].EffectiveMode);
        Assert.Equal(EffectiveDisplayMode.Blackout, windowManager.ReceivedStates[3].EffectiveMode);
    }

    [Fact]
    public async Task CierreExternoActualizaIsOpenSinLanzarExcepcionesAlEnviarEstado()
    {
        // Arrange
        var windowManager = new InMemoryProjectorWindowManager();
        var broadcaster = new ChannelLiveBroadcaster();
        var liveManager = new LiveStateManager([broadcaster]);
        using var projectorService = new ProjectorService(windowManager, liveManager, broadcaster);
        await projectorService.OpenOnMonitorAsync("mon-2");

        // Act
        windowManager.SimulateExternalClose();
        await liveManager.ProjectSlideAsync(new SlideContentBuilder().Build());
        await Task.Delay(30);

        // Assert
        Assert.False(windowManager.IsOpen);
        Assert.Single(windowManager.ReceivedStates); // Solo el snapshot inicial antes del cierre externo
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!predicate() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
    }
}
