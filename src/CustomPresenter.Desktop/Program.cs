using CustomPresenter.Application.Announcements;
using CustomPresenter.Application.Backup;
using CustomPresenter.Application.Bibles;
using CustomPresenter.Application.Live;
using CustomPresenter.Application.Media;
using CustomPresenter.Application.Playlists;
using CustomPresenter.Application.Songs;
using CustomPresenter.Application.Themes;
using CustomPresenter.Infrastructure.Backup;
using CustomPresenter.Infrastructure.Importers;
using CustomPresenter.Infrastructure.Media;
using CustomPresenter.Infrastructure.ObsServer;
using CustomPresenter.Infrastructure.Sqlite;

namespace CustomPresenter.Desktop;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var options = ObsServerOptions.FromEnvironment();
        if (args.Contains("--healthcheck", StringComparer.OrdinalIgnoreCase))
        {
            return RunHealthcheckAsync(options.Port).GetAwaiter().GetResult();
        }

        RunAsync(options).GetAwaiter().GetResult();
        return 0;
    }

    private static async Task<int> RunHealthcheckAsync(int port)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            using var response = await http
                .GetAsync(new Uri($"http://127.0.0.1:{port}/api/live"))
                .ConfigureAwait(false);
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return 1;
        }
    }

    private static async Task RunAsync(ObsServerOptions options)
    {
        var dataRoot = Environment.GetEnvironmentVariable("CUSTOM_PRESENTER_DATA_DIR");
        if (string.IsNullOrWhiteSpace(dataRoot))
        {
            dataRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CustomPresenter");
        }

        Directory.CreateDirectory(dataRoot);
        var dbPath = Path.Combine(dataRoot, "presenter.db");
        var mediaRoot = Path.Combine(dataRoot, "media");
        Directory.CreateDirectory(mediaRoot);

        var connectionString = $"Data Source={dbPath};Pooling=False";
        await SqliteSchemaInitializer.EnsureCreatedAsync(connectionString).ConfigureAwait(false);

        var songRepo = new SqliteSongRepository(connectionString);
        var bibleRepo = new SqliteBibleRepository(connectionString);
        var announcementRepo = new SqliteAnnouncementRepository(connectionString);
        var playlistRepo = new SqlitePlaylistRepository(connectionString);
        var themeRepo = new SqliteThemeRepository(connectionString);
        var mediaRepo = new SqliteMediaRepository(connectionString);
        var mediaStorage = new LocalMediaStorage(mediaRoot);

        var themeService = new ThemeService(themeRepo);
        var services = new PresenterServices(
            new SongService(songRepo, new SongImporter()),
            new BibleImportService(bibleRepo, new BibleImporter()),
            new BibleSearchService(bibleRepo),
            new AnnouncementService(announcementRepo),
            new PlaylistService(playlistRepo, songRepo, bibleRepo, announcementRepo),
            themeService,
            new MediaLibraryService(mediaRepo, mediaStorage),
            new BackupService(songRepo, announcementRepo, new JsonBackupSerializer()),
            mediaRepo,
            mediaStorage,
            ResolveStaticWebRoot());

        var broadcaster = new ChannelLiveBroadcaster();
        var liveManager = new LiveStateManager([broadcaster]);
        await themeService.LoadAndApplyAsync(liveManager).ConfigureAwait(false);

        using var shutdown = new CancellationTokenSource();
        Console.CancelKeyPress += (_, args) =>
        {
            args.Cancel = true;
            shutdown.Cancel();
        };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => shutdown.Cancel();

        await using var obsServer = new ObsWebServer(
            options,
            liveManager,
            broadcaster,
            services);

        await obsServer.StartAsync(shutdown.Token).ConfigureAwait(false);
        Console.WriteLine($"Custom Presenter escuchando en {obsServer.BaseUri}");

        try
        {
            await Task.Delay(Timeout.Infinite, shutdown.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Apagado limpio solicitado.
        }
    }

    private static string? ResolveStaticWebRoot()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "wwwroot"),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "frontend", "dist")),
        };

        return candidates.FirstOrDefault(Directory.Exists);
    }
}
