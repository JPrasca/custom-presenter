namespace CustomPresenter.Domain.Themes;

/// <summary>
/// Puerto de persistencia para los temas visuales independientes del Proyector y OBS.
/// </summary>
public interface IThemeRepository
{
    Task<ThemeSettings> GetAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(ThemeSettings settings, CancellationToken cancellationToken = default);
}
