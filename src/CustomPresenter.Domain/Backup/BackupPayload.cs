namespace CustomPresenter.Domain.Backup;

/// <summary>
/// Representación serializable de una canción en el archivo de respaldo local.
/// </summary>
public sealed record BackupSongDto(
    Guid Id,
    string Title,
    string? Author,
    string? MusicalKey,
    string RawLyrics,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

/// <summary>
/// Representación serializable de un anuncio en el archivo de respaldo local.
/// </summary>
public sealed record BackupAnnouncementDto(
    Guid Id,
    string Title,
    string RawBody,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

/// <summary>
/// Contenedor raíz del respaldo JSON local de canciones y anuncios.
/// </summary>
public sealed record BackupPayload(
    int SchemaVersion,
    DateTimeOffset ExportedAtUtc,
    IReadOnlyList<BackupSongDto> Songs,
    IReadOnlyList<BackupAnnouncementDto> Announcements)
{
    public const int CurrentSchemaVersion = 1;
    public const int MaxBackupBytes = 50 * 1024 * 1024;
}

/// <summary>
/// Resumen del resultado de una importación de respaldo local.
/// </summary>
public sealed record BackupRestoreSummary(
    int ImportedSongs,
    int SkippedSongs,
    int ImportedAnnouncements,
    int SkippedAnnouncements);
