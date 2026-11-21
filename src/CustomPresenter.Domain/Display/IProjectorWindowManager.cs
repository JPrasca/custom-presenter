using CustomPresenter.Domain.Live;

namespace CustomPresenter.Domain.Display;

/// <summary>
/// Puerto para controlar la detección de monitores y el ciclo de vida de la ventana de proyección.
/// </summary>
public interface IProjectorWindowManager
{
    /// <summary>
    /// Indica si la ventana de proyección acepta comandos IPC de escritura (siempre false por seguridad).
    /// </summary>
    bool AllowsWriteIpcHandlers { get; }

    bool IsOpen { get; }

    MonitorInfo? ActiveMonitor { get; }

    IReadOnlyList<MonitorInfo> GetMonitors();

    Task OpenAsync(
        MonitorInfo targetMonitor,
        ProjectorDisplayMode displayMode,
        CancellationToken cancellationToken = default);

    Task CloseAsync(CancellationToken cancellationToken = default);

    ValueTask SendLiveStateAsync(LiveState state, CancellationToken cancellationToken = default);
}
