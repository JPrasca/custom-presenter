using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CustomPresenter.Domain.Bibles;

/// <summary>
/// Representación estructurada de una cita bíblica (ej. Salmos 23:1-3 o I Corintios 13:4).
/// </summary>
public sealed record BibleReference(
    string BookQuery,
    int? CanonicalBookNumber,
    int Chapter,
    int? StartVerse,
    int? EndVerse);

/// <summary>
/// Parser de referencias bíblicas en español con soporte de abreviaturas, tildes, números romanos y rangos.
/// </summary>
public static partial class BibleReferenceParser
{
    private static readonly Dictionary<string, int> CanonicalBooks = BuildCanonicalBookMap();

    private static readonly string[] CanonicalNames =
    [
        "Génesis", "Éxodo", "Levítico", "Números", "Deuteronomio",
        "Josué", "Jueces", "Rut", "1 Samuel", "2 Samuel",
        "1 Reyes", "2 Reyes", "1 Crónicas", "2 Crónicas", "Esdras",
        "Nehemías", "Ester", "Job", "Salmos", "Proverbios",
        "Eclesiastés", "Cantares", "Isaías", "Jeremías", "Lamentaciones",
        "Ezequiel", "Daniel", "Oseas", "Joel", "Amós",
        "Abdías", "Jonás", "Miqueas", "Nahúm", "Habacuc",
        "Sofonías", "Hageo", "Zacarías", "Malaquías", "Mateo",
        "Marcos", "Lucas", "Juan", "Hechos", "Romanos",
        "1 Corintios", "2 Corintios", "Gálatas", "Efesios", "Filipenses",
        "Colosenses", "1 Tesalonicenses", "2 Tesalonicenses", "1 Timoteo", "2 Timoteo",
        "Tito", "Filemón", "Hebreos", "Santiago", "1 Pedro",
        "2 Pedro", "1 Juan", "2 Juan", "3 Juan", "Judas",
        "Apocalipsis"
    ];

    [GeneratedRegex(
        @"^\s*(?<book>(?:(?:III|II|I)\s+|(?:[123])\s*)?[\p{L}]+(?:\s+[\p{L}]+)*)\s+(?<chapter>\d{1,3})(?::(?<start>\d{1,3})(?:-(?<end>\d{1,3}))?)?\s*$",
        RegexOptions.IgnoreCase)]
    private static partial Regex ReferenceRegex();

    public static bool TryParse(string? input, out BibleReference? reference)
    {
        reference = null;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var match = ReferenceRegex().Match(input);
        if (!match.Success)
        {
            return false;
        }

        var rawBook = match.Groups["book"].Value.Trim();
        if (!int.TryParse(match.Groups["chapter"].Value, CultureInfo.InvariantCulture, out var chapter)
            || chapter <= 0)
        {
            return false;
        }

        int? startVerse = null;
        if (match.Groups["start"].Success)
        {
            if (!int.TryParse(match.Groups["start"].Value, CultureInfo.InvariantCulture, out var parsedStart)
                || parsedStart <= 0)
            {
                return false;
            }

            startVerse = parsedStart;
        }

        int? endVerse = null;
        if (match.Groups["end"].Success)
        {
            if (!int.TryParse(match.Groups["end"].Value, CultureInfo.InvariantCulture, out var parsedEnd)
                || startVerse is null
                || parsedEnd < startVerse.Value)
            {
                return false;
            }

            endVerse = parsedEnd;
        }

        var normalizedBook = NormalizeBookKey(rawBook);
        var canonicalNumber = CanonicalBooks.TryGetValue(normalizedBook, out var number)
            ? number
            : (int?)null;

        reference = new BibleReference(rawBook, canonicalNumber, chapter, startVerse, endVerse);
        return true;
    }

    public static string? GetCanonicalBookName(int bookNumber)
    {
        return bookNumber is >= 1 and <= 66
            ? CanonicalNames[bookNumber - 1]
            : null;
    }

    public static int? ResolveCanonicalBookNumber(string bookName)
    {
        if (string.IsNullOrWhiteSpace(bookName))
        {
            return null;
        }

        var normalized = NormalizeBookKey(bookName).Replace(" ", string.Empty, StringComparison.Ordinal);
        return CanonicalBooks.TryGetValue(normalized, out var number) ? number : null;
    }

    public static string NormalizeBookKey(string bookName)
    {
        if (string.IsNullOrWhiteSpace(bookName))
        {
            return string.Empty;
        }

        var withoutAccents = RemoveDiacritics(bookName.Trim()).ToLowerInvariant();
        var collapsed = string.Join(' ', withoutAccents.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        if (collapsed.StartsWith("iii ", StringComparison.Ordinal))
        {
            collapsed = $"3 {collapsed[4..]}";
        }
        else if (collapsed.StartsWith("ii ", StringComparison.Ordinal))
        {
            collapsed = $"2 {collapsed[3..]}";
        }
        else if (collapsed.StartsWith("i ", StringComparison.Ordinal))
        {
            collapsed = $"1 {collapsed[2..]}";
        }

        return collapsed.Replace(" ", string.Empty, StringComparison.Ordinal);
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static Dictionary<string, int> BuildCanonicalBookMap()
    {
        var entries = new (int Number, string[] Aliases)[]
        {
            (1, ["genesis", "gen", "gn"]),
            (2, ["exodo", "ex"]),
            (3, ["levitico", "lev", "lv"]),
            (4, ["numeros", "num", "nm"]),
            (5, ["deuteronomio", "deut", "dt"]),
            (6, ["josue", "jos"]),
            (7, ["jueces", "jue"]),
            (8, ["rut", "rt"]),
            (9, ["1samuel", "1sam", "1s"]),
            (10, ["2samuel", "2sam", "2s"]),
            (11, ["1reyes", "1rey", "1r"]),
            (12, ["2reyes", "2rey", "2r"]),
            (13, ["1cronicas", "1cron", "1cr"]),
            (14, ["2cronicas", "2cron", "2cr"]),
            (15, ["esdras", "esd"]),
            (16, ["nehemias", "neh"]),
            (17, ["ester", "est"]),
            (18, ["job"]),
            (19, ["salmos", "salmo", "sal", "sl"]),
            (20, ["proverbios", "prov", "pr"]),
            (21, ["eclesiastes", "ecl", "ec"]),
            (22, ["cantares", "cant", "cnt"]),
            (23, ["isaias", "is"]),
            (24, ["jeremias", "jer", "jr"]),
            (25, ["lamentaciones", "lam", "lm"]),
            (26, ["ezequiel", "ez"]),
            (27, ["daniel", "dan", "dn"]),
            (28, ["oseas", "os"]),
            (29, ["joel", "jl"]),
            (30, ["amos", "am"]),
            (31, ["abdias", "abd"]),
            (32, ["jonas", "jon"]),
            (33, ["miqueas", "miq", "mi"]),
            (34, ["nahum", "nah"]),
            (35, ["habacuc", "hab"]),
            (36, ["sofonias", "sof"]),
            (37, ["hageo", "hag"]),
            (38, ["zacarias", "zac"]),
            (39, ["malaquias", "mal"]),
            (40, ["mateo", "mat", "mt"]),
            (41, ["marcos", "mar", "mc", "mr"]),
            (42, ["lucas", "luc", "lc"]),
            (43, ["juan", "jn"]),
            (44, ["hechos", "hch"]),
            (45, ["romanos", "rom", "ro"]),
            (46, ["1corintios", "1cor", "1co"]),
            (47, ["2corintios", "2cor", "2co"]),
            (48, ["galatas", "gal", "ga"]),
            (49, ["efesios", "ef"]),
            (50, ["filipenses", "fil", "flp"]),
            (51, ["colosenses", "col"]),
            (52, ["1tesalonicenses", "1tes", "1ts"]),
            (53, ["2tesalonicenses", "2tes", "2ts"]),
            (54, ["1timoteo", "1tim", "1ti"]),
            (55, ["2timoteo", "2tim", "2ti"]),
            (56, ["tito", "tit"]),
            (57, ["filemon", "flm"]),
            (58, ["hebreos", "heb"]),
            (59, ["santiago", "stg"]),
            (60, ["1pedro", "1ped", "1pe", "1p"]),
            (61, ["2pedro", "2ped", "2pe", "2p"]),
            (62, ["1juan", "1jn"]),
            (63, ["2juan", "2jn"]),
            (64, ["3juan", "3jn"]),
            (65, ["judas", "jud"]),
            (66, ["apocalipsis", "apoc", "ap"]),
        };

        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (number, aliases) in entries)
        {
            foreach (var alias in aliases)
            {
                map[alias] = number;
            }
        }

        return map;
    }
}
