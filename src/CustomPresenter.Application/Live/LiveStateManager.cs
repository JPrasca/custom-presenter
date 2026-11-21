using CustomPresenter.Domain.Live;
using CustomPresenter.Domain.Media;
using CustomPresenter.Domain.Ports;
using CustomPresenter.Domain.Themes;

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
    public Task<LiveState> ProjectSlideAsync(
        SlideContent slide,
        CancellationToken cancellationToken = default) =>
        UpdateStateAsync(
            state => LiveState.FromSlide(slide, state.EffectiveTheme, state.BackgroundMedia),
            cancellationToken);

    /// <summary>
    /// Actualiza los temas visuales activos y notifica en tiempo real sin perder la diapositiva.
    /// </summary>
    public Task<LiveState> UpdateThemeAsync(
        ThemeSettings theme,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(theme);
        return UpdateStateAsync(state => state.WithTheme(theme), cancellationToken);
    }

    /// <summary>
    /// Asigna o limpia el fondo multimedia activo en el Proyector Nativo.
    /// </summary>
    public Task<LiveState> SetBackgroundMediaAsync(
        BackgroundMediaReference? backgroundMedia,
        CancellationToken cancellationToken = default) =>
        UpdateStateAsync(state => state.WithBackgroundMedia(backgroundMedia), cancellationToken);

    /// <summary>
    /// Alterna la bandera de limpiar texto manteniendo la diapositiva activa.
    /// </summary>
    public Task<LiveState> ToggleClearTextAsync(CancellationToken cancellationToken = default) =>
        UpdateStateAsync(state => state.WithToggleClearText(), cancellationToken);

    /// <summary>
    /// Alterna la bandera de pantalla negra (blackout).
    /// </summary>
    public Task<LiveState> ToggleBlackoutAsync(CancellationToken cancellationToken = default) =>
        UpdateStateAsync(state => state.WithToggleBlackout(), cancellationToken);

    /// <summary>
    /// Alterna la bandera de logo/reposo.
    /// </summary>
    public Task<LiveState> ToggleLogoAsync(CancellationToken cancellationToken = default) =>
        UpdateStateAsync(state => state.WithToggleLogo(), cancellationToken);

    private async Task<LiveState> UpdateStateAsync(
        Func<LiveState, LiveState> transition,
        CancellationToken cancellationToken)
    {
        LiveState updated;
        lock (_sync)
        {
            updated = transition(_state);
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
