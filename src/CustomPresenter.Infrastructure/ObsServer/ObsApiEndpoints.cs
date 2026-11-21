using System.Text.Json;
using CustomPresenter.Application.Live;
using CustomPresenter.Domain.Backup;
using CustomPresenter.Domain.Bibles;
using CustomPresenter.Domain.Live;
using CustomPresenter.Domain.Media;
using CustomPresenter.Domain.Playlists;
using CustomPresenter.Domain.Themes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace CustomPresenter.Infrastructure.ObsServer;

internal static class ObsApiEndpoints
{
    public static void MapPresenterEndpoints(
        WebApplication app,
        PresenterServices services,
        LiveStateManager liveStateManager,
        JsonSerializerOptions jsonOptions,
        Func<HttpRequest, bool> isAuthorizedOperator)
    {
        MapSongEndpoints(app, services, liveStateManager, jsonOptions, isAuthorizedOperator);
        MapBibleEndpoints(app, services, liveStateManager, jsonOptions, isAuthorizedOperator);
        MapAnnouncementEndpoints(app, services, liveStateManager, jsonOptions, isAuthorizedOperator);
        MapPlaylistEndpoints(app, services, liveStateManager, jsonOptions, isAuthorizedOperator);
        MapThemeEndpoints(app, services, liveStateManager, jsonOptions, isAuthorizedOperator);
        MapMediaEndpoints(app, services, liveStateManager, jsonOptions, isAuthorizedOperator);
        MapBackupEndpoints(app, services, jsonOptions, isAuthorizedOperator);
        MapStaticFrontend(app, services.StaticWebRoot);
    }

    private static void MapSongEndpoints(
        WebApplication app,
        PresenterServices services,
        LiveStateManager liveStateManager,
        JsonSerializerOptions jsonOptions,
        Func<HttpRequest, bool> isAuthorized)
    {
        app.MapGet("/api/songs", async (string? q, CancellationToken ct) =>
        {
            var songs = string.IsNullOrWhiteSpace(q)
                ? await services.Songs.ListSongsAsync(ct).ConfigureAwait(false)
                : await services.Songs.SearchSongsAsync(q, 50, ct).ConfigureAwait(false);
            return Results.Json(songs, jsonOptions);
        });

        app.MapPost("/api/songs", (HttpContext ctx) => ExecuteOperatorAsync(ctx, isAuthorized, jsonOptions, async () =>
        {
            var req = await ReadJsonAsync<CreateSongRequest>(ctx, jsonOptions).ConfigureAwait(false);
            var song = await services.Songs.CreateSongAsync(
                req.Title,
                req.Author,
                req.MusicalKey,
                req.RawLyrics,
                ctx.RequestAborted).ConfigureAwait(false);
            return Results.Json(song, jsonOptions);
        }));

        app.MapDelete("/api/songs/{id:guid}", (HttpContext ctx, Guid id) => ExecuteOperatorAsync(ctx, isAuthorized, jsonOptions, async () =>
        {
            var deleted = await services.Songs.DeleteSongAsync(id, ctx.RequestAborted).ConfigureAwait(false);
            return deleted ? Results.Ok(new { deleted = true }) : Results.NotFound();
        }));

        app.MapPost("/api/songs/{id:guid}/project/{slideIndex:int}", (HttpContext ctx, Guid id, int slideIndex) =>
            ExecuteOperatorAsync(ctx, isAuthorized, jsonOptions, async () =>
            {
                var song = await services.Songs.GetSongAsync(id, ctx.RequestAborted).ConfigureAwait(false)
                    ?? throw new DomainException($"No se encontró la canción '{id}'.");
                var slides = Application.Songs.SongService.ToSlideContents(song);
                if (slideIndex < 0 || slideIndex >= slides.Count)
                {
                    throw new DomainException($"Índice de estrofa {slideIndex} fuera de rango.");
                }

                var state = await liveStateManager.ProjectSlideAsync(slides[slideIndex], ctx.RequestAborted).ConfigureAwait(false);
                return Results.Json(state, jsonOptions);
            }));
    }

    private static void MapBibleEndpoints(
        WebApplication app,
        PresenterServices services,
        LiveStateManager liveStateManager,
        JsonSerializerOptions jsonOptions,
        Func<HttpRequest, bool> isAuthorized)
    {
        app.MapGet("/api/bibles/versions", async (CancellationToken ct) =>
            Results.Json(await services.BibleImport.ListVersionsAsync(ct).ConfigureAwait(false), jsonOptions));

        app.MapGet("/api/bibles/search", async (Guid versionId, string? q, CancellationToken ct) =>
            Results.Json(
                await services.BibleSearch.SearchAsync(versionId, q ?? string.Empty, 50, ct).ConfigureAwait(false),
                jsonOptions));

        app.MapPost("/api/bibles/import", (HttpContext ctx) => ExecuteOperatorAsync(ctx, isAuthorized, jsonOptions, async () =>
        {
            SetMaxBodySize(ctx, 50L * 1024 * 1024);
            var form = await ctx.Request.ReadFormAsync(ctx.RequestAborted).ConfigureAwait(false);
            var file = form.Files.GetFile("file")
                ?? throw new DomainException("Debe adjuntar un archivo XML de Biblia.");
            var name = form["name"].ToString();
            var abbr = form["abbreviation"].ToString();

            await using var stream = file.OpenReadStream();
            var result = await services.BibleImport.ImportBibleAsync(
                stream,
                file.FileName,
                file.Length,
                name,
                abbr,
                ctx.RequestAborted).ConfigureAwait(false);

            return Results.Json(result.Version, jsonOptions);
        }));

        app.MapPost("/api/bibles/project", (HttpContext ctx) => ExecuteOperatorAsync(ctx, isAuthorized, jsonOptions, async () =>
        {
            var req = await ReadJsonAsync<ProjectBibleRequest>(ctx, jsonOptions).ConfigureAwait(false);
            var verse = new BibleVerse(
                req.VersionId,
                req.BookNumber,
                req.BookName,
                req.Chapter,
                req.Verse,
                req.Text);
            var slide = Application.Bibles.BibleSearchService.ToSlideContent(verse, req.VersionAbbreviation ?? string.Empty);
            var state = await liveStateManager.ProjectSlideAsync(slide, ctx.RequestAborted).ConfigureAwait(false);
            return Results.Json(state, jsonOptions);
        }));
    }

    private static void MapAnnouncementEndpoints(
        WebApplication app,
        PresenterServices services,
        LiveStateManager liveStateManager,
        JsonSerializerOptions jsonOptions,
        Func<HttpRequest, bool> isAuthorized)
    {
        app.MapGet("/api/announcements", async (CancellationToken ct) =>
            Results.Json(await services.Announcements.ListAllAsync(ct).ConfigureAwait(false), jsonOptions));

        app.MapPost("/api/announcements", (HttpContext ctx) => ExecuteOperatorAsync(ctx, isAuthorized, jsonOptions, async () =>
        {
            var req = await ReadJsonAsync<CreateAnnouncementRequest>(ctx, jsonOptions).ConfigureAwait(false);
            var created = await services.Announcements.CreateAsync(req.Title, req.RawBody, ctx.RequestAborted).ConfigureAwait(false);
            return Results.Json(created, jsonOptions);
        }));

        app.MapDelete("/api/announcements/{id:guid}", (HttpContext ctx, Guid id) => ExecuteOperatorAsync(ctx, isAuthorized, jsonOptions, async () =>
        {
            var deleted = await services.Announcements.DeleteAsync(id, ctx.RequestAborted).ConfigureAwait(false);
            return deleted ? Results.Ok(new { deleted = true }) : Results.NotFound();
        }));

        app.MapPost("/api/announcements/{id:guid}/project/{slideIndex:int}", (HttpContext ctx, Guid id, int slideIndex) =>
            ExecuteOperatorAsync(ctx, isAuthorized, jsonOptions, async () =>
            {
                var state = await services.Announcements.ProjectSlideAsync(id, slideIndex, liveStateManager, ctx.RequestAborted)
                    .ConfigureAwait(false);
                return Results.Json(state, jsonOptions);
            }));
    }

    private static void MapPlaylistEndpoints(
        WebApplication app,
        PresenterServices services,
        LiveStateManager liveStateManager,
        JsonSerializerOptions jsonOptions,
        Func<HttpRequest, bool> isAuthorized)
    {
        app.MapGet("/api/playlists", async (CancellationToken ct) =>
            Results.Json(await services.Playlists.ListAllAsync(ct).ConfigureAwait(false), jsonOptions));

        app.MapPost("/api/playlists", (HttpContext ctx) => ExecuteOperatorAsync(ctx, isAuthorized, jsonOptions, async () =>
        {
            var req = await ReadJsonAsync<CreatePlaylistRequest>(ctx, jsonOptions).ConfigureAwait(false);
            var created = await services.Playlists.CreateAsync(req.Name, ctx.RequestAborted).ConfigureAwait(false);
            return Results.Json(created, jsonOptions);
        }));

        app.MapPost("/api/playlists/{id:guid}/items", (HttpContext ctx, Guid id) => ExecuteOperatorAsync(ctx, isAuthorized, jsonOptions, async () =>
        {
            var req = await ReadJsonAsync<AddPlaylistItemRequest>(ctx, jsonOptions).ConfigureAwait(false);
            Playlist updated = req.Kind switch
            {
                "Song" when req.SongId is not null =>
                    await services.Playlists.AddSongAsync(id, req.SongId.Value, ctx.RequestAborted).ConfigureAwait(false),
                "Announcement" when req.AnnouncementId is not null =>
                    await services.Playlists.AddAnnouncementAsync(id, req.AnnouncementId.Value, ctx.RequestAborted).ConfigureAwait(false),
                "BibleVerse" when req.VersionId is not null && req.BookName is not null =>
                    await services.Playlists.AddBiblePassageAsync(
                        id,
                        req.VersionId.Value,
                        req.BookNumber ?? 1,
                        req.BookName,
                        req.Chapter ?? 1,
                        req.StartVerse ?? 1,
                        req.EndVerse,
                        ctx.RequestAborted).ConfigureAwait(false),
                _ => throw new DomainException("Solicitud de ítem de playlist inválida."),
            };

            return Results.Json(updated, jsonOptions);
        }));

        app.MapPut("/api/playlists/{id:guid}/reorder", (HttpContext ctx, Guid id) => ExecuteOperatorAsync(ctx, isAuthorized, jsonOptions, async () =>
        {
            var req = await ReadJsonAsync<ReorderPlaylistRequest>(ctx, jsonOptions).ConfigureAwait(false);
            var updated = await services.Playlists.ReorderItemsAsync(id, req.OrderedItemIds, ctx.RequestAborted)
                .ConfigureAwait(false);
            return Results.Json(updated, jsonOptions);
        }));

        app.MapPost("/api/playlists/{id:guid}/project/{itemIndex:int}", (HttpContext ctx, Guid id, int itemIndex) =>
            ExecuteOperatorAsync(ctx, isAuthorized, jsonOptions, async () =>
            {
                var state = await services.Playlists.ProjectItemAsync(id, itemIndex, 0, liveStateManager, ctx.RequestAborted)
                    .ConfigureAwait(false);
                return Results.Json(state, jsonOptions);
            }));
    }

    private static void MapThemeEndpoints(
        WebApplication app,
        PresenterServices services,
        LiveStateManager liveStateManager,
        JsonSerializerOptions jsonOptions,
        Func<HttpRequest, bool> isAuthorized)
    {
        app.MapGet("/api/themes", async (CancellationToken ct) =>
            Results.Json(await services.Themes.GetCurrentAsync(ct).ConfigureAwait(false), jsonOptions));

        app.MapPut("/api/themes/projector", (HttpContext ctx) => ExecuteOperatorAsync(ctx, isAuthorized, jsonOptions, async () =>
        {
            var req = await ReadJsonAsync<ProjectorThemeRequest>(ctx, jsonOptions).ConfigureAwait(false);
            var theme = ProjectorTheme.Create(req.FontFamily, req.FontSizePx, req.TextColor, req.BackgroundColor, req.Alignment);
            var state = await services.Themes.UpdateProjectorThemeAsync(theme, liveStateManager, ctx.RequestAborted)
                .ConfigureAwait(false);
            return Results.Json(state, jsonOptions);
        }));

        app.MapPut("/api/themes/obs", (HttpContext ctx) => ExecuteOperatorAsync(ctx, isAuthorized, jsonOptions, async () =>
        {
            var req = await ReadJsonAsync<ObsThemeRequest>(ctx, jsonOptions).ConfigureAwait(false);
            var theme = ObsTheme.Create(
                req.FontFamily,
                req.FontSizePx,
                req.TextColor,
                req.BoxBackgroundColor,
                req.Position,
                req.MarginHorizontalPx,
                req.MarginVerticalPx,
                req.BorderRadiusPx);
            var state = await services.Themes.UpdateObsThemeAsync(theme, liveStateManager, ctx.RequestAborted)
                .ConfigureAwait(false);
            return Results.Json(state, jsonOptions);
        }));
    }

    private static void MapMediaEndpoints(
        WebApplication app,
        PresenterServices services,
        LiveStateManager liveStateManager,
        JsonSerializerOptions jsonOptions,
        Func<HttpRequest, bool> isAuthorized)
    {
        app.MapGet("/api/media", async (CancellationToken ct) =>
            Results.Json(await services.Media.ListAllAsync(ct).ConfigureAwait(false), jsonOptions));

        app.MapPost("/api/media", (HttpContext ctx) => ExecuteOperatorAsync(ctx, isAuthorized, jsonOptions, async () =>
        {
            SetMaxBodySize(ctx, MediaConstants.MaxVideoBytes);
            var form = await ctx.Request.ReadFormAsync(ctx.RequestAborted).ConfigureAwait(false);
            var file = form.Files.GetFile("file")
                ?? throw new DomainException("Debe adjuntar un archivo multimedia.");

            await using var stream = file.OpenReadStream();
            var asset = await services.Media.ImportAsync(stream, file.FileName, ctx.RequestAborted).ConfigureAwait(false);
            return Results.Json(asset, jsonOptions);
        }));

        app.MapPost("/api/media/{id:guid}/assign", (HttpContext ctx, Guid id) => ExecuteOperatorAsync(ctx, isAuthorized, jsonOptions, async () =>
        {
            var state = await services.Media.AssignBackgroundAsync(id, liveStateManager, ctx.RequestAborted)
                .ConfigureAwait(false);
            return Results.Json(state, jsonOptions);
        }));

        app.MapPost("/api/media/clear", (HttpContext ctx) => ExecuteOperatorAsync(ctx, isAuthorized, jsonOptions, async () =>
        {
            var state = await Application.Media.MediaLibraryService.ClearBackgroundAsync(liveStateManager, ctx.RequestAborted)
                .ConfigureAwait(false);
            return Results.Json(state, jsonOptions);
        }));

        app.MapDelete("/api/media/{id:guid}", (HttpContext ctx, Guid id) => ExecuteOperatorAsync(ctx, isAuthorized, jsonOptions, async () =>
        {
            var deleted = await services.Media.DeleteAsync(id, liveStateManager, ctx.RequestAborted).ConfigureAwait(false);
            return deleted ? Results.Ok(new { deleted = true }) : Results.NotFound();
        }));
    }

    private static void MapBackupEndpoints(
        WebApplication app,
        PresenterServices services,
        JsonSerializerOptions jsonOptions,
        Func<HttpRequest, bool> isAuthorized)
    {
        app.MapGet("/api/backup/export", async (CancellationToken ct) =>
        {
            await using var ms = new MemoryStream();
            await services.Backup.ExportAsync(ms, ct).ConfigureAwait(false);
            return Results.File(ms.ToArray(), "application/json; charset=utf-8", "custom-presenter-backup.json");
        });

        app.MapPost("/api/backup/import", (HttpContext ctx) => ExecuteOperatorAsync(ctx, isAuthorized, jsonOptions, async () =>
        {
            SetMaxBodySize(ctx, BackupPayload.MaxBackupBytes);
            var summary = await services.Backup.ImportAsync(ctx.Request.Body, ctx.RequestAborted).ConfigureAwait(false);
            return Results.Json(summary, jsonOptions);
        }));
    }

    private static void MapStaticFrontend(WebApplication app, string? staticWebRoot)
    {
        if (string.IsNullOrWhiteSpace(staticWebRoot) || !Directory.Exists(staticWebRoot))
        {
            return;
        }

        var rootFull = Path.GetFullPath(staticWebRoot);
        var rootWithSep = rootFull.EndsWith(Path.DirectorySeparatorChar)
            ? rootFull
            : rootFull + Path.DirectorySeparatorChar;

        IResult ServeIndex()
        {
            var indexPath = Path.GetFullPath(Path.Combine(rootFull, "index.html"));
            if (!indexPath.StartsWith(rootWithSep, StringComparison.Ordinal) || !File.Exists(indexPath))
            {
                return Results.NotFound();
            }

            return Results.File(indexPath, "text/html; charset=utf-8");
        }

        app.MapGet("/", ServeIndex);
        app.MapGet("/projector", ServeIndex);

        var assetsRoot = Path.GetFullPath(Path.Combine(rootFull, "assets"));
        var assetMap = Directory.Exists(assetsRoot)
            ? Directory.EnumerateFiles(assetsRoot, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetFullPath(path))
                .Where(fullPath => fullPath.StartsWith(assetsRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                .ToDictionary(
                    fullPath => Path.GetRelativePath(assetsRoot, fullPath).Replace('\\', '/'),
                    fullPath => fullPath,
                    StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal);

        app.MapGet("/assets/{**filePath}", (string filePath) =>
        {
            if (string.IsNullOrWhiteSpace(filePath)
                || !assetMap.TryGetValue(filePath.TrimStart('/'), out var safePhysicalPath))
            {
                return Results.NotFound();
            }

            return Results.File(safePhysicalPath, GetMimeType(safePhysicalPath));
        });
    }

    private static string GetMimeType(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".js" or ".mjs" => "application/javascript; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".svg" => "image/svg+xml",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".woff2" => "font/woff2",
            _ => "application/octet-stream",
        };

    private static void SetMaxBodySize(HttpContext ctx, long maxBytes)
    {
        var feature = ctx.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (feature is not null && !feature.IsReadOnly)
        {
            feature.MaxRequestBodySize = maxBytes;
        }
    }

    private static async Task ExecuteOperatorAsync(
        HttpContext ctx,
        Func<HttpRequest, bool> isAuthorized,
        JsonSerializerOptions jsonOptions,
        Func<Task<IResult>> action)
    {
        if (!isAuthorized(ctx.Request))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        try
        {
            var result = await action().ConfigureAwait(false);
            await result.ExecuteAsync(ctx).ConfigureAwait(false);
        }
        catch (DomainException ex)
        {
            await Results.BadRequest(new { error = ex.Message }).ExecuteAsync(ctx).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            await Results.BadRequest(new { error = "Payload JSON inválido." }).ExecuteAsync(ctx).ConfigureAwait(false);
        }
    }

    private static async Task<T> ReadJsonAsync<T>(HttpContext ctx, JsonSerializerOptions jsonOptions)
    {
        var dto = await JsonSerializer.DeserializeAsync<T>(ctx.Request.Body, jsonOptions, ctx.RequestAborted)
            .ConfigureAwait(false);
        return dto ?? throw new DomainException("El cuerpo de la solicitud JSON está vacío.");
    }

    private sealed record CreateSongRequest(string Title, string RawLyrics, string? Author, string? MusicalKey);
    private sealed record ProjectBibleRequest(Guid VersionId, int BookNumber, string BookName, int Chapter, int Verse, string Text, string? VersionAbbreviation);
    private sealed record CreateAnnouncementRequest(string Title, string RawBody);
    private sealed record CreatePlaylistRequest(string Name);
    private sealed record AddPlaylistItemRequest(string Kind, Guid? SongId, Guid? AnnouncementId, Guid? VersionId, int? BookNumber, string? BookName, int? Chapter, int? StartVerse, int? EndVerse);
    private sealed record ReorderPlaylistRequest(IReadOnlyList<Guid> OrderedItemIds);
    private sealed record ProjectorThemeRequest(string FontFamily, int FontSizePx, string TextColor, string BackgroundColor, TextAlignment Alignment);
    private sealed record ObsThemeRequest(string FontFamily, int FontSizePx, string TextColor, string BoxBackgroundColor, LowerThirdPosition Position, int MarginHorizontalPx, int MarginVerticalPx, int BorderRadiusPx);
}
