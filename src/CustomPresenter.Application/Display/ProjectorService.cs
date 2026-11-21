using CustomPresenter.Application.Live;
using CustomPresenter.Domain.Display;
using CustomPresenter.Domain.Live;

namespace CustomPresenter.Application.Display;

/// <summary>
/// Caso de uso para listar monitores, abrir/cerrar la ventana de proyección y sincronizarla en vivo.
/// </summary>
public sealed class ProjectorService : IDisposable
{
    private readonly IProjectorWindowManager _windowManager;
    private readonly LiveStateManager _liveStateManager;
    private readonly LiveSubscription? _subscription;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task? _relayTask;
    private int _disposed;

    public ProjectorService(
        IProjectorWindowManager windowManager,
        LiveStateManager liveStateManager,
        ChannelLiveBroadcaster? broadcaster = null)
    {
        ArgumentNullException.ThrowIfNull(windowManager);
        ArgumentNullException.ThrowIfNull(liveStateManager);

        _windowManager = windowManager;
        _liveStateManager = liveStateManager;

        if (broadcaster is not null)
        {
            _subscription = broadcaster.Subscribe();
            _relayTask = Task.Run(() => RelayLoopAsync(_subscription, _cts.Token));
        }
    }

    public IReadOnlyList<MonitorInfo> GetAvailableMonitors() => _windowManager.GetMonitors();

    public async Task OpenOnMonitorAsync(
        string monitorId,
        bool forceWindowed = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(monitorId))
        {
            throw new DomainException("Debe seleccionar un monitor válido.");
        }

        var monitors = _windowManager.GetMonitors();
        var target = monitors.FirstOrDefault(m => string.Equals(m.Id, monitorId, StringComparison.Ordinal))
            ?? throw new DomainException($"No se encontró el monitor con identificador '{monitorId}'.");

        target.Validate();

        var displayMode = forceWindowed || monitors.Count <= 1
            ? ProjectorDisplayMode.Windowed
            : ProjectorDisplayMode.BorderlessFullscreen;

        await _windowManager.OpenAsync(target, displayMode, cancellationToken).ConfigureAwait(false);
        await _windowManager.SendLiveStateAsync(_liveStateManager.Snapshot(), cancellationToken)
            .ConfigureAwait(false);
    }

    public Task CloseProjectorAsync(CancellationToken cancellationToken = default) =>
        _windowManager.CloseAsync(cancellationToken);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _cts.Cancel();
        _subscription?.Dispose();

        try
        {
            _relayTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException)
        {
            // Ignora cancelación esperada al cerrar el servicio.
        }
        finally
        {
            _cts.Dispose();
        }
    }

    private async Task RelayLoopAsync(LiveSubscription subscription, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var state in subscription.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                if (_windowManager.IsOpen)
                {
                    await _windowManager.SendLiveStateAsync(state, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Cierre ordenado del servicio.
        }
    }
}
