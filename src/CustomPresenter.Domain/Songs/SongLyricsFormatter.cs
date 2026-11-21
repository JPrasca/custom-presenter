using System.Text.RegularExpressions;

namespace CustomPresenter.Domain.Songs;

/// <summary>
/// Resultado de búsqueda de letra en línea ya formateado en estrofas para proyección.
/// </summary>
public sealed record LyricsSearchResult(
    string TrackName,
    string ArtistName,
    string FormattedLyrics);

/// <summary>
/// Puerto para consultar letras de canciones en proveedores externos permitidos.
/// </summary>
public interface ILyricsLookupClient
{
    Task<IReadOnlyList<LyricsSearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Formatea letras crudas o sincronizadas LRC en estrofas limpias de máximo 4 líneas para proyección.
/// </summary>
public static partial class SongLyricsFormatter
{
    public const int DefaultMaxLinesPerSlide = 4;

    [GeneratedRegex(@"\[\d{2}:\d{2}(?:\.\d{2,3})?\]", RegexOptions.CultureInvariant)]
    private static partial Regex NumericTimestampRegex();

    [GeneratedRegex(@"\n\s*\n+", RegexOptions.CultureInvariant)]
    private static partial Regex BlankLineRegex();

    public static string Format(string rawLyrics, int maxLinesPerSlide = DefaultMaxLinesPerSlide)
    {
        if (string.IsNullOrWhiteSpace(rawLyrics))
        {
            return string.Empty;
        }

        var effectiveMax = Math.Max(1, maxLinesPerSlide);
        var normalized = NumericTimestampRegex()
            .Replace(rawLyrics, string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        var blocks = BlankLineRegex().Split(normalized);
        var formattedBlocks = new List<string>();

        foreach (var block in blocks)
        {
            var lines = block
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .ToArray();

            if (lines.Length == 0)
            {
                continue;
            }

            for (var i = 0; i < lines.Length; i += effectiveMax)
            {
                var count = Math.Min(effectiveMax, lines.Length - i);
                formattedBlocks.Add(string.Join('\n', lines, i, count));
            }
        }

        return string.Join("\n\n", formattedBlocks);
    }
}
