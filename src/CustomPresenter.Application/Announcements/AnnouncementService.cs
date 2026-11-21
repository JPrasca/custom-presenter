using CustomPresenter.Application.Live;
using CustomPresenter.Domain.Announcements;
using CustomPresenter.Domain.Live;

namespace CustomPresenter.Application.Announcements;

/// <summary>
/// Casos de uso CRUD y proyección directa de anuncios o textos predefinidos.
/// </summary>
public sealed class AnnouncementService
{
    private readonly IAnnouncementRepository _repository;

    public AnnouncementService(IAnnouncementRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    public async Task<Announcement> CreateAsync(
        string title,
        string rawBody,
        CancellationToken cancellationToken = default)
    {
        var announcement = Announcement.Create(title, rawBody);
        await _repository.CreateAsync(announcement, cancellationToken).ConfigureAwait(false);
        return announcement;
    }

    public async Task<Announcement> UpdateAsync(
        Guid id,
        string title,
        string rawBody,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException($"No se encontró el anuncio con identificador '{id}'.");

        var updated = existing.WithUpdatedContent(title, rawBody);
        var saved = await _repository.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);
        if (!saved)
        {
            throw new DomainException($"No se pudo actualizar el anuncio '{id}'.");
        }

        return updated;
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.DeleteAsync(id, cancellationToken);

    public Task<Announcement?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<Announcement>> ListAllAsync(CancellationToken cancellationToken = default) =>
        _repository.ListAllAsync(cancellationToken);

    public async Task<LiveState> ProjectSlideAsync(
        Guid announcementId,
        int slideIndex,
        LiveStateManager liveStateManager,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(liveStateManager);

        var announcement = await _repository.GetByIdAsync(announcementId, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException($"No se encontró el anuncio con identificador '{announcementId}'.");

        if (slideIndex < 0 || slideIndex >= announcement.Slides.Count)
        {
            throw new DomainException(
                $"El índice de bloque {slideIndex} está fuera de rango para el anuncio '{announcement.Title}'.");
        }

        var slides = ToSlideContents(announcement);
        return await liveStateManager.ProjectSlideAsync(slides[slideIndex], cancellationToken)
            .ConfigureAwait(false);
    }

    public static IReadOnlyList<SlideContent> ToSlideContents(Announcement announcement)
    {
        ArgumentNullException.ThrowIfNull(announcement);

        return announcement.Slides
            .Select(slide => new SlideContent(
                $"announcement:{announcement.Id}:slide:{slide.Index}",
                announcement.Title,
                slide.Text,
                announcement.Title))
            .ToList();
    }
}
