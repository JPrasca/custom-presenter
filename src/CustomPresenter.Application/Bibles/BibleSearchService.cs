using CustomPresenter.Domain.Bibles;
using CustomPresenter.Domain.Live;
using CustomPresenter.Domain.Songs;

namespace CustomPresenter.Application.Bibles;

/// <summary>
/// Casos de uso de navegación y búsqueda bíblica por referencia o texto completo (FTS5).
/// </summary>
public sealed class BibleSearchService
{
    private readonly IBibleRepository _repository;
    private readonly IBibleOnlineProvider? _onlineProvider;

    public BibleSearchService(
        IBibleRepository repository,
        IBibleOnlineProvider? onlineProvider = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
        _onlineProvider = onlineProvider;
    }

    public Task<IReadOnlyList<BibleBookSummary>> ListBooksAsync(
        Guid versionId,
        CancellationToken cancellationToken = default) =>
        _repository.ListBooksAsync(versionId, cancellationToken);

    public async Task<IReadOnlyList<BibleVerse>> GetChapterVersesAsync(
        Guid versionId,
        int bookNumber,
        int chapter,
        CancellationToken cancellationToken = default)
    {
        var local = await _repository.GetChapterVersesAsync(versionId, bookNumber, chapter, cancellationToken).ConfigureAwait(false);
        if (local.Count > 0 || _onlineProvider is null)
        {
            return local;
        }

        var versions = await _repository.ListVersionsAsync(cancellationToken).ConfigureAwait(false);
        var currentVersion = versions.FirstOrDefault(v => v.Id == versionId);
        var abbr = currentVersion?.Abbreviation ?? "RVR60";
        var bookName = BibleReferenceParser.GetCanonicalBookName(bookNumber) ?? $"Libro {bookNumber}";

        try
        {
            var onlineVerses = await _onlineProvider.FetchChapterVersesAsync(
                versionId,
                abbr,
                bookNumber,
                bookName,
                chapter,
                cancellationToken).ConfigureAwait(false);

            if (onlineVerses.Count > 0)
            {
                await _repository.SaveVersesBatchAsync(onlineVerses, cancellationToken).ConfigureAwait(false);
                return onlineVerses;
            }
        }
        catch
        {
            // Falla controlada sin afectar modo offline
        }

        return [];
    }

    public async Task<IReadOnlyList<BibleVerse>> SearchAsync(
        Guid versionId,
        string query,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        if (BibleReferenceParser.TryParse(query, out var reference) && reference is not null)
        {
            var local = await _repository.FindByReferenceAsync(versionId, reference, cancellationToken).ConfigureAwait(false);
            if (local.Count > 0 || _onlineProvider is null)
            {
                return local;
            }

            var versions = await _repository.ListVersionsAsync(cancellationToken).ConfigureAwait(false);
            var currentVersion = versions.FirstOrDefault(v => v.Id == versionId);
            var abbr = currentVersion?.Abbreviation ?? "RVR60";
            var bookNumber = reference.CanonicalBookNumber ?? 1;
            var bookName = BibleReferenceParser.GetCanonicalBookName(bookNumber) ?? reference.BookQuery;

            try
            {
                var onlineVerses = await _onlineProvider.FetchChapterVersesAsync(
                    versionId,
                    abbr,
                    bookNumber,
                    bookName,
                    reference.Chapter,
                    cancellationToken).ConfigureAwait(false);

                if (onlineVerses.Count > 0)
                {
                    await _repository.SaveVersesBatchAsync(onlineVerses, cancellationToken).ConfigureAwait(false);
                    return await _repository.FindByReferenceAsync(versionId, reference, cancellationToken).ConfigureAwait(false);
                }
            }
            catch
            {
                // Falla controlada sin afectar modo offline
            }

            return [];
        }

        var sanitized = Fts5QuerySanitizer.Sanitize(query);
        if (sanitized.Length == 0)
        {
            return [];
        }

        return await _repository.SearchVersesFtsAsync(
            versionId,
            sanitized,
            Math.Clamp(limit, 1, 200),
            cancellationToken).ConfigureAwait(false);
    }

    public static SlideContent ToSlideContent(BibleVerse verse, string versionAbbreviation)
    {
        ArgumentNullException.ThrowIfNull(verse);
        var formattedRef = FormatReference(
            verse.BookName,
            verse.Chapter,
            verse.Verse,
            null,
            versionAbbreviation);

        return new SlideContent(
            $"{verse.VersionId}:{verse.BookNumber}:{verse.Chapter}:{verse.Verse}",
            formattedRef,
            verse.Text,
            formattedRef);
    }

    public static SlideContent ToSlideContent(
        IReadOnlyList<BibleVerse> verses,
        string versionAbbreviation)
    {
        ArgumentNullException.ThrowIfNull(verses);
        if (verses.Count == 0)
        {
            throw new DomainException("Debe seleccionar al menos un versículo para proyectar.");
        }

        if (verses.Count == 1)
        {
            return ToSlideContent(verses[0], versionAbbreviation);
        }

        var first = verses[0];
        var last = verses[^1];
        var formattedRef = FormatReference(
            first.BookName,
            first.Chapter,
            first.Verse,
            last.Verse,
            versionAbbreviation);

        var combinedText = string.Join(
            '\n',
            verses.Select(v => $"{v.Verse} {v.Text}"));

        return new SlideContent(
            $"{first.VersionId}:{first.BookNumber}:{first.Chapter}:{first.Verse}-{last.Verse}",
            formattedRef,
            combinedText,
            formattedRef);
    }

    private static string FormatReference(
        string bookName,
        int chapter,
        int startVerse,
        int? endVerse,
        string versionAbbreviation)
    {
        var versePart = endVerse is not null && endVerse.Value != startVerse
            ? $"{startVerse}-{endVerse.Value}"
            : startVerse.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var abbr = string.IsNullOrWhiteSpace(versionAbbreviation)
            ? string.Empty
            : $" ({versionAbbreviation.Trim()})";

        return $"{bookName} {chapter}:{versePart}{abbr}";
    }
}
