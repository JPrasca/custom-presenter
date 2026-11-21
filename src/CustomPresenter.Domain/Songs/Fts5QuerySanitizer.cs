using System.Text.RegularExpressions;

namespace CustomPresenter.Domain.Songs;

/// <summary>
/// Sanitiza consultas de texto libre para prevenir errores de sintaxis e inyección de operadores en SQLite FTS5.
/// </summary>
public static partial class Fts5QuerySanitizer
{
    private const int MaxTokens = 12;

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordTokenRegex();

    public static string Sanitize(string? rawQuery)
    {
        if (string.IsNullOrWhiteSpace(rawQuery))
        {
            return string.Empty;
        }

        var tokens = WordTokenRegex()
            .Matches(rawQuery)
            .Select(match => $"\"{match.Value}\"*")
            .Take(MaxTokens)
            .ToArray();

        return tokens.Length == 0 ? string.Empty : string.Join(' ', tokens);
    }
}
