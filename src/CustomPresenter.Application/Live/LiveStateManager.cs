using CustomPresenter.Domain.Live;
using CustomPresenter.Domain.Ports;

namespace CustomPresenter.Application.Live;

/// <summary>
/// Gestor central de estado en vivo en la capa de Aplicación.
/// </summary>
public sealed class LiveStateManager
{
    private readonly Lock _sync = new();
    private readonly IReadOnlyList<ILiveBroadcaster> _broadcasters;
    private LiveState _state = LiveState.Empty;

    public LiveStateManager(IEnumerable<ILiveBroadcaster> broadcasters)
    {
        ArgumentNullException.ThrowIfNull(broadcasters);
        _broadcasters = broadcasters.ToList();
    }

    /// <summary>
    /// Devuelve una instantánea del estado actual en vivo.
    /// </summary>
    public LiveState Snapshot()
    {
        lock (_sync)
        {
            return _state;
        }
    }

    /// <summary>
    /// Proyecta una diapositiva y notifica a todos los suscriptores.
    /// </summary>
    public async Task<LiveState> ProjectSlideAsync(
        SlideContent slide,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(slide);

        if (string.IsNullOrWhiteSpace(slide.Text))
        {
            throw new DomainException("El contenido de la diapositiva no puede estar vacío.");
        }

        LiveState updated;
        lock (_sync)
        {
            updated = new LiveState(
                slide,
                new LiveFlags(ClearText: false, Blackout: false, Logo: false));
            _state = updated;
        }

        await NotifyAsync(updated, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    /// <summary>
    /// Alterna la bandera de limpiar texto manteniendo la diapositiva activa.
    /// </summary>
    public async Task<LiveState> ToggleClearTextAsync(CancellationToken cancellationToken = default)
    {
        LiveState updated;
        lock (_sync)
        {
            updated = _state with
            {
                Flags = _state.Flags with { ClearText = !_state.Flags.ClearText },
            };
            _state = updated;
        }

        await NotifyAsync(updated, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    /// <summary>
    /// Alterna la bandera de pantalla negra (blackout).
    /// </summary>
    public async Task<LiveState> ToggleBlackoutAsync(CancellationToken cancellationToken = default)
    {
        LiveState updated;
        lock (_sync)
        {
            updated = _state with
            {
                Flags = _state.Flags with { Blackout = !_state.Flags.Blackout },
            };
            _state = updated;
        }

        await NotifyAsync(updated, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    private async Task NotifyAsync(LiveState state, CancellationToken cancellationToken)
    {
        foreach (var broadcaster in _broadcasters)
        {
            await broadcaster.BroadcastAsync(state, cancellationToken).ConfigureAwait(false);
        }
    }
}
