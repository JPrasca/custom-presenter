using System.Text.RegularExpressions;
using CustomPresenter.Domain.Live;

namespace CustomPresenter.Domain.Songs;

/// <summary>
/// Diapositiva individual derivada de una estrofa o bloque de canción.
/// </summary>
public sealed record SongSlide(
    int Index,
    string Label,
    string Text);

/// <summary>
/// Algoritmo puro de dominio para dividir letras en diapositivas por líneas en blanco.
/// </summary>
public static partial class SongLyricsSplitter
{
    [GeneratedRegex(@"\n[ \t]*\n+")]
    private static partial Regex BlankLineSeparatorRegex();

    public static IReadOnlyList<SongSlide> Split(string rawLyrics)
    {
        if (string.IsNullOrWhiteSpace(rawLyrics))
        {
            throw new DomainException("La letra de la canción no puede estar vacía.");
        }

        var normalized = rawLyrics
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        var blocks = BlankLineSeparatorRegex().Split(normalized);
        var slides = new List<SongSlide>();

        foreach (var block in blocks)
        {
            var cleanedLines = block
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .ToArray();

            if (cleanedLines.Length == 0)
            {
                continue;
            }

            var text = string.Join('\n', cleanedLines);
            if (text.Length > SlideContent.MaxTextLength)
            {
                throw new DomainException(
                    $"Una estrofa excede el máximo permitido de {SlideContent.MaxTextLength} caracteres.");
            }

            var index = slides.Count;
            slides.Add(new SongSlide(index, $"Diapositiva {index + 1}", text));
        }

        if (slides.Count == 0)
        {
            throw new DomainException("La canción debe contener al menos una diapositiva válida.");
        }

        return slides;
    }
}

/// <summary>
/// Entidad de canción con su letra original y diapositivas ordenadas.
/// </summary>
public sealed record Song(
    Guid Id,
    string Title,
    string? Author,
    string? MusicalKey,
    string RawLyrics,
    IReadOnlyList<SongSlide> Slides,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    public const int MaxTitleLength = 200;
    public const int MaxRawLyricsLength = 100_000;

    public static Song Create(
        string title,
        string? author,
        string? musicalKey,
        string rawLyrics)
    {
        var normalizedTitle = NormalizeAndValidateTitle(title);
        var normalizedLyrics = ValidateRawLyrics(rawLyrics);
        var slides = SongLyricsSplitter.Split(normalizedLyrics);
        var now = DateTimeOffset.UtcNow;

        return new Song(
            Guid.NewGuid(),
            normalizedTitle,
            NormalizeOptional(author),
            NormalizeOptional(musicalKey),
            normalizedLyrics,
            slides,
            now,
            now);
    }

    public Song WithUpdatedContent(
        string title,
        string? author,
        string? musicalKey,
        string rawLyrics)
    {
        var normalizedTitle = NormalizeAndValidateTitle(title);
        var normalizedLyrics = ValidateRawLyrics(rawLyrics);
        var slides = SongLyricsSplitter.Split(normalizedLyrics);

        return this with
        {
            Title = normalizedTitle,
            Author = NormalizeOptional(author),
            MusicalKey = NormalizeOptional(musicalKey),
            RawLyrics = normalizedLyrics,
            Slides = slides,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    private static string NormalizeAndValidateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("El título de la canción no puede estar vacío.");
        }

        var trimmed = title.Trim();
        if (trimmed.Length > MaxTitleLength)
        {
            throw new DomainException(
                $"El título de la canción excede el máximo de {MaxTitleLength} caracteres.");
        }

        return trimmed;
    }

    private static string ValidateRawLyrics(string rawLyrics)
    {
        if (string.IsNullOrWhiteSpace(rawLyrics))
        {
            throw new DomainException("La letra de la canción no puede estar vacía.");
        }

        if (rawLyrics.Length > MaxRawLyricsLength)
        {
            throw new DomainException(
                $"La letra excede el máximo permitido de {MaxRawLyricsLength} caracteres.");
        }

        return rawLyrics.Trim();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
