namespace CustomPresenter.Domain.Live;

/// <summary>
/// Contenido de una diapositiva lista para proyectar.
/// </summary>
public sealed record SlideContent(
    string Id,
    string Title,
    string Text,
    string? Reference = null);

/// <summary>
/// Banderas globales de estado en vivo.
/// </summary>
public sealed record LiveFlags(
    bool ClearText = false,
    bool Blackout = false,
    bool Logo = false);

/// <summary>
/// Estado único de proyección en vivo (Single Source of Truth).
/// </summary>
public sealed record LiveState(
    SlideContent? CurrentSlide,
    LiveFlags Flags)
{
    public static LiveState Empty { get; } = new(null, new LiveFlags());

    /// <summary>
    /// Determina si el texto de la diapositiva debe mostrarse en las salidas.
    /// </summary>
    public bool IsTextVisible =>
        CurrentSlide is not null
        && !Flags.ClearText
        && !Flags.Blackout
        && !Flags.Logo;
}

/// <summary>
/// Excepción para violaciones de reglas puras del dominio.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException()
    {
    }

    public DomainException(string message)
        : base(message)
    {
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
