namespace CustomPresenter.Domain.Live;

/// <summary>
/// Modo efectivo de visualización considerando la prioridad entre banderas.
/// </summary>
public enum EffectiveDisplayMode
{
    Idle = 0,
    Slide = 1,
    ClearText = 2,
    Logo = 3,
    Blackout = 4,
}

/// <summary>
/// Contenido de una diapositiva lista para proyectar.
/// </summary>
public sealed record SlideContent(
    string Id,
    string Title,
    string Text,
    string? Reference = null)
{
    public const int MaxTextLength = 10_000;

    /// <summary>
    /// Valida las invariantes de dominio de la diapositiva.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new DomainException("El identificador de la diapositiva no puede estar vacío.");
        }

        if (string.IsNullOrWhiteSpace(Text))
        {
            throw new DomainException("El contenido de la diapositiva no puede estar vacío.");
        }

        if (Text.Length > MaxTextLength)
        {
            throw new DomainException(
                $"El contenido de la diapositiva excede el máximo permitido de {MaxTextLength} caracteres.");
        }
    }
}

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
    /// Calcula el modo efectivo respetando la jerarquía Blackout &gt; Logo &gt; ClearText &gt; Slide &gt; Idle.
    /// </summary>
    public EffectiveDisplayMode EffectiveMode
    {
        get
        {
            if (Flags.Blackout)
            {
                return EffectiveDisplayMode.Blackout;
            }

            if (Flags.Logo)
            {
                return EffectiveDisplayMode.Logo;
            }

            if (Flags.ClearText)
            {
                return EffectiveDisplayMode.ClearText;
            }

            return CurrentSlide is not null
                ? EffectiveDisplayMode.Slide
                : EffectiveDisplayMode.Idle;
        }
    }

    /// <summary>
    /// Determina si el texto de la diapositiva debe mostrarse en las salidas.
    /// </summary>
    public bool IsTextVisible => EffectiveMode == EffectiveDisplayMode.Slide;

    public static LiveState FromSlide(SlideContent slide)
    {
        ArgumentNullException.ThrowIfNull(slide);
        slide.Validate();
        return new LiveState(slide, new LiveFlags(ClearText: false, Blackout: false, Logo: false));
    }

    public LiveState WithToggleClearText() =>
        this with { Flags = Flags with { ClearText = !Flags.ClearText } };

    public LiveState WithToggleBlackout() =>
        this with { Flags = Flags with { Blackout = !Flags.Blackout } };

    public LiveState WithToggleLogo() =>
        this with { Flags = Flags with { Logo = !Flags.Logo } };
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
