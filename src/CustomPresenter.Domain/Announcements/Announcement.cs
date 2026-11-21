using CustomPresenter.Domain.Live;
using CustomPresenter.Domain.Songs;

namespace CustomPresenter.Domain.Announcements;

/// <summary>
/// Diapositiva individual perteneciente a un anuncio o texto predefinido.
/// </summary>
public sealed record AnnouncementSlide(
    int Index,
    string Text);

/// <summary>
/// Entidad de anuncio o texto predefinido reutilizable para eventos en vivo.
/// </summary>
public sealed record Announcement(
    Guid Id,
    string Title,
    string RawBody,
    IReadOnlyList<AnnouncementSlide> Slides,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    public const int MaxTitleLength = 200;
    public const int MaxRawBodyLength = 50_000;

    public static Announcement Create(string title, string rawBody)
    {
        var normalizedTitle = NormalizeAndValidateTitle(title);
        var normalizedBody = ValidateRawBody(rawBody);
        var slides = SplitSlides(normalizedBody);
        var now = DateTimeOffset.UtcNow;

        return new Announcement(
            Guid.NewGuid(),
            normalizedTitle,
            normalizedBody,
            slides,
            now,
            now);
    }

    public Announcement WithUpdatedContent(string title, string rawBody)
    {
        var normalizedTitle = NormalizeAndValidateTitle(title);
        var normalizedBody = ValidateRawBody(rawBody);
        var slides = SplitSlides(normalizedBody);

        return this with
        {
            Title = normalizedTitle,
            RawBody = normalizedBody,
            Slides = slides,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    private static List<AnnouncementSlide> SplitSlides(string rawBody) =>
        SongLyricsSplitter.Split(rawBody)
            .Select(slide => new AnnouncementSlide(slide.Index, slide.Text))
            .ToList();

    private static string NormalizeAndValidateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("El título del anuncio no puede estar vacío.");
        }

        var trimmed = title.Trim();
        if (trimmed.Length > MaxTitleLength)
        {
            throw new DomainException(
                $"El título del anuncio excede el máximo de {MaxTitleLength} caracteres.");
        }

        return trimmed;
    }

    private static string ValidateRawBody(string rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            throw new DomainException("El contenido del anuncio no puede estar vacío.");
        }

        if (rawBody.Length > MaxRawBodyLength)
        {
            throw new DomainException(
                $"El contenido del anuncio excede el máximo permitido de {MaxRawBodyLength} caracteres.");
        }

        return rawBody.Trim();
    }
}
