using CustomPresenter.Application.Announcements;
using CustomPresenter.Application.Backup;
using CustomPresenter.Application.Bibles;
using CustomPresenter.Application.Media;
using CustomPresenter.Application.Playlists;
using CustomPresenter.Application.Songs;
using CustomPresenter.Application.Themes;
using CustomPresenter.Domain.Media;

namespace CustomPresenter.Infrastructure.ObsServer;

/// <summary>
/// Agrupa los servicios de aplicación e infraestructura expuestos por el servidor local.
/// </summary>
public sealed record PresenterServices(
    SongService Songs,
    BibleImportService BibleImport,
    BibleSearchService BibleSearch,
    AnnouncementService Announcements,
    PlaylistService Playlists,
    ThemeService Themes,
    MediaLibraryService Media,
    BackupService Backup,
    IMediaRepository MediaRepository,
    IMediaStorage MediaStorage,
    string? StaticWebRoot = null);
