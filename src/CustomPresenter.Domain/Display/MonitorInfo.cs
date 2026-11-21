using CustomPresenter.Domain.Live;

namespace CustomPresenter.Domain.Display;

/// <summary>
/// Modo de visualización de la ventana del proyector nativo.
/// </summary>
public enum ProjectorDisplayMode
{
    Windowed = 0,
    BorderlessFullscreen = 1,
}

/// <summary>
/// Información de un monitor o pantalla física detectada en el sistema.
/// </summary>
public sealed record MonitorInfo(
    string Id,
    string Name,
    int X,
    int Y,
    int Width,
    int Height,
    bool IsPrimary)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new DomainException("El identificador del monitor no puede estar vacío.");
        }

        if (Width <= 0 || Height <= 0)
        {
            throw new DomainException("La resolución del monitor debe ser mayor a cero.");
        }
    }
}
