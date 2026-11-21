using CustomPresenter.Application.Live;
using CustomPresenter.Domain.Live;
using CustomPresenter.Domain.Themes;

namespace CustomPresenter.Application.Themes;

/// <summary>
/// Casos de uso para gestionar temas visuales independientes (Proyector vs OBS) y propagarlos en vivo.
/// </summary>
public sealed class ThemeService
{
    private readonly IThemeRepository _repository;

    public ThemeService(IThemeRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    public Task<ThemeSettings> GetCurrentAsync(CancellationToken cancellationToken = default) =>
        _repository.GetAsync(cancellationToken);

    public async Task<LiveState> LoadAndApplyAsync(
        LiveStateManager liveStateManager,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(liveStateManager);
        var settings = await _repository.GetAsync(cancellationToken).ConfigureAwait(false);
        return await liveStateManager.UpdateThemeAsync(settings, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LiveState> UpdateProjectorThemeAsync(
        ProjectorTheme projectorTheme,
        LiveStateManager liveStateManager,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projectorTheme);
        ArgumentNullException.ThrowIfNull(liveStateManager);

        var current = await _repository.GetAsync(cancellationToken).ConfigureAwait(false);
        var updated = current with { Projector = projectorTheme };
        await _repository.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
        return await liveStateManager.UpdateThemeAsync(updated, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LiveState> UpdateObsThemeAsync(
        ObsTheme obsTheme,
        LiveStateManager liveStateManager,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(obsTheme);
        ArgumentNullException.ThrowIfNull(liveStateManager);

        var current = await _repository.GetAsync(cancellationToken).ConfigureAwait(false);
        var updated = current with { Obs = obsTheme };
        await _repository.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
        return await liveStateManager.UpdateThemeAsync(updated, cancellationToken).ConfigureAwait(false);
    }
}
