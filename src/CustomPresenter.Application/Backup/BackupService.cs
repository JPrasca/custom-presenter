using CustomPresenter.Domain.Announcements;
using CustomPresenter.Domain.Backup;
using CustomPresenter.Domain.Live;
using CustomPresenter.Domain.Songs;

namespace CustomPresenter.Application.Backup;

/// <summary>
/// Casos de uso de exportación e importación idempotente de respaldos locales en JSON.
/// </summary>
public sealed class BackupService
{
    private readonly ISongRepository _songRepository;
    private readonly IAnnouncementRepository _announcementRepository;
    private readonly IBackupSerializer _serializer;

    public BackupService(
        ISongRepository songRepository,
        IAnnouncementRepository announcementRepository,
        IBackupSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(songRepository);
        ArgumentNullException.ThrowIfNull(announcementRepository);
        ArgumentNullException.ThrowIfNull(serializer);

        _songRepository = songRepository;
        _announcementRepository = announcementRepository;
        _serializer = serializer;
    }

    public async Task<BackupPayload> ExportAsync(
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);

        var songs = await _songRepository.ListAllAsync(cancellationToken).ConfigureAwait(false);
        var announcements = await _announcementRepository.ListAllAsync(cancellationToken).ConfigureAwait(false);

        var payload = new BackupPayload(
            BackupPayload.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            songs.Select(s => new BackupSongDto(
                s.Id,
                s.Title,
                s.Author,
                s.MusicalKey,
                s.RawLyrics,
                s.CreatedAtUtc,
                s.UpdatedAtUtc)).ToList(),
            announcements.Select(a => new BackupAnnouncementDto(
                a.Id,
                a.Title,
                a.RawBody,
                a.CreatedAtUtc,
                a.UpdatedAtUtc)).ToList());

        await _serializer.SerializeAsync(payload, destination, cancellationToken).ConfigureAwait(false);
        return payload;
    }

    public async Task<BackupRestoreSummary> ImportAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var payload = await _serializer.DeserializeAsync(source, cancellationToken).ConfigureAwait(false);

        var existingSongs = await _songRepository.ListAllAsync(cancellationToken).ConfigureAwait(false);
        var existingAnnouncements = await _announcementRepository.ListAllAsync(cancellationToken).ConfigureAwait(false);

        var seenSongIds = existingSongs.Select(s => s.Id).ToHashSet();
        var seenSongTitles = existingSongs.Select(s => s.Title).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var seenAnnouncementIds = existingAnnouncements.Select(a => a.Id).ToHashSet();
        var seenAnnouncementTitles = existingAnnouncements.Select(a => a.Title).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var songsToImport = new List<Song>();
        var skippedSongs = 0;

        // Fase 1: Validar TODOS los registros (fail-fast) y deduplicar antes de persistir.
        foreach (var dto in payload.Songs)
        {
            var validated = BuildValidatedSong(dto);
            if (!seenSongIds.Add(validated.Id) || !seenSongTitles.Add(validated.Title))
            {
                skippedSongs++;
                continue;
            }

            songsToImport.Add(validated);
        }

        var announcementsToImport = new List<Announcement>();
        var skippedAnnouncements = 0;

        foreach (var dto in payload.Announcements)
        {
            var validated = BuildValidatedAnnouncement(dto);
            if (!seenAnnouncementIds.Add(validated.Id) || !seenAnnouncementTitles.Add(validated.Title))
            {
                skippedAnnouncements++;
                continue;
            }

            announcementsToImport.Add(validated);
        }

        // Fase 2: Persistir solo cuando todo el respaldo pasó la validación de dominio.
        if (songsToImport.Count > 0)
        {
            await _songRepository.CreateBatchAsync(songsToImport, cancellationToken).ConfigureAwait(false);
        }

        foreach (var announcement in announcementsToImport)
        {
            await _announcementRepository.CreateAsync(announcement, cancellationToken).ConfigureAwait(false);
        }

        return new BackupRestoreSummary(
            songsToImport.Count,
            skippedSongs,
            announcementsToImport.Count,
            skippedAnnouncements);
    }

    private static Song BuildValidatedSong(BackupSongDto dto)
    {
        if (dto is null || dto.Id == Guid.Empty)
        {
            throw new DomainException("El respaldo contiene una canción con identificador inválido.");
        }

        var validated = Song.Create(dto.Title, dto.Author, dto.MusicalKey, dto.RawLyrics);
        return validated with
        {
            Id = dto.Id,
            CreatedAtUtc = dto.CreatedAtUtc == default ? validated.CreatedAtUtc : dto.CreatedAtUtc,
            UpdatedAtUtc = dto.UpdatedAtUtc == default ? validated.UpdatedAtUtc : dto.UpdatedAtUtc,
        };
    }

    private static Announcement BuildValidatedAnnouncement(BackupAnnouncementDto dto)
    {
        if (dto is null || dto.Id == Guid.Empty)
        {
            throw new DomainException("El respaldo contiene un anuncio con identificador inválido.");
        }

        var validated = Announcement.Create(dto.Title, dto.RawBody);
        return validated with
        {
            Id = dto.Id,
            CreatedAtUtc = dto.CreatedAtUtc == default ? validated.CreatedAtUtc : dto.CreatedAtUtc,
            UpdatedAtUtc = dto.UpdatedAtUtc == default ? validated.UpdatedAtUtc : dto.UpdatedAtUtc,
        };
    }
}
