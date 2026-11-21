using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CustomPresenter.Domain.Live;

namespace CustomPresenter.Domain.Themes;

/// <summary>
/// Destino visual al que aplica una configuración de tema.
/// </summary>
public enum ThemeTarget
{
    Projector = 1,
    ObsLowerThird = 2,
}

/// <summary>
/// Alineación horizontal de texto permitida.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TextAlignment
{
    Left = 0,
    Center = 1,
    Right = 2,
}

/// <summary>
/// Posición vertical del overlay lower third en OBS.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LowerThirdPosition
{
    Bottom = 0,
    Middle = 1,
    Top = 2,
}

internal static partial class ThemeValidation
{
    public const int MaxFontFamilyLength = 120;

    [GeneratedRegex("^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$", RegexOptions.CultureInvariant)]
    private static partial Regex HexColorRegex();

    [GeneratedRegex("^[a-zA-Z0-9\\s,\\-_'\"]+$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeFontFamilyRegex();

    public static string ValidateFontFamily(string fontFamily)
    {
        if (string.IsNullOrWhiteSpace(fontFamily))
        {
            throw new DomainException("La familia tipográfica no puede estar vacía.");
        }

        var trimmed = fontFamily.Trim();
        if (trimmed.Length > MaxFontFamilyLength || !SafeFontFamilyRegex().IsMatch(trimmed))
        {
            throw new DomainException(
                "La familia tipográfica contiene caracteres no permitidos o excede la longitud máxima.");
        }

        return trimmed;
    }

    public static string ValidateHexColor(string color, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(color) || !HexColorRegex().IsMatch(color.Trim()))
        {
            throw new DomainException(
                $"El campo '{fieldName}' debe ser un color hexadecimal válido (#RRGGBB o #RRGGBBAA).");
        }

        return color.Trim().ToUpperInvariant();
    }

    public static int ValidateRange(int value, int min, int max, string fieldName)
    {
        if (value < min || value > max)
        {
            throw new DomainException(
                $"El campo '{fieldName}' ({value}) debe estar entre {min} y {max}.");
        }

        return value;
    }
}

/// <summary>
/// Configuración visual independiente para la ventana del Proyector Nativo.
/// </summary>
public sealed record ProjectorTheme(
    string FontFamily,
    int FontSizePx,
    string TextColor,
    string BackgroundColor,
    TextAlignment Alignment)
{
    public static ProjectorTheme Default { get; } = new(
        "system-ui, -apple-system, sans-serif",
        64,
        "#FFFFFF",
        "#0B0D12",
        TextAlignment.Center);

    public static ProjectorTheme Create(
        string fontFamily,
        int fontSizePx,
        string textColor,
        string backgroundColor,
        TextAlignment alignment)
    {
        if (!Enum.IsDefined(alignment))
        {
            throw new DomainException("Alineación de texto inválida para el proyector.");
        }

        return new ProjectorTheme(
            ThemeValidation.ValidateFontFamily(fontFamily),
            ThemeValidation.ValidateRange(fontSizePx, 16, 160, nameof(FontSizePx)),
            ThemeValidation.ValidateHexColor(textColor, nameof(TextColor)),
            ThemeValidation.ValidateHexColor(backgroundColor, nameof(BackgroundColor)),
            alignment);
    }
}

/// <summary>
/// Configuración visual independiente para el overlay Lower Third de OBS (/obs).
/// </summary>
public sealed record ObsTheme(
    string FontFamily,
    int FontSizePx,
    string TextColor,
    string BoxBackgroundColor,
    LowerThirdPosition Position,
    int MarginHorizontalPx,
    int MarginVerticalPx,
    int BorderRadiusPx)
{
    public static ObsTheme Default { get; } = new(
        "system-ui, -apple-system, sans-serif",
        34,
        "#FFFFFF",
        "#0F1115D1",
        LowerThirdPosition.Bottom,
        64,
        48,
        10);

    public static ObsTheme Create(
        string fontFamily,
        int fontSizePx,
        string textColor,
        string boxBackgroundColor,
        LowerThirdPosition position,
        int marginHorizontalPx,
        int marginVerticalPx,
        int borderRadiusPx)
    {
        if (!Enum.IsDefined(position))
        {
            throw new DomainException("Posición inválida para el overlay de OBS.");
        }

        return new ObsTheme(
            ThemeValidation.ValidateFontFamily(fontFamily),
            ThemeValidation.ValidateRange(fontSizePx, 14, 120, nameof(FontSizePx)),
            ThemeValidation.ValidateHexColor(textColor, nameof(TextColor)),
            ThemeValidation.ValidateHexColor(boxBackgroundColor, nameof(BoxBackgroundColor)),
            position,
            ThemeValidation.ValidateRange(marginHorizontalPx, 0, 300, nameof(MarginHorizontalPx)),
            ThemeValidation.ValidateRange(marginVerticalPx, 0, 300, nameof(MarginVerticalPx)),
            ThemeValidation.ValidateRange(borderRadiusPx, 0, 64, nameof(BorderRadiusPx)));
    }
}

/// <summary>
/// Conjunto de temas independientes para Proyector Nativo y OBS Overlay.
/// </summary>
public sealed record ThemeSettings(
    ProjectorTheme Projector,
    ObsTheme Obs)
{
    public static ThemeSettings Default { get; } = new(ProjectorTheme.Default, ObsTheme.Default);
}
