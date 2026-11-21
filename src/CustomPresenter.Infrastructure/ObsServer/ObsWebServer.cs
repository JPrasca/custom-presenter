using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using CustomPresenter.Application.Live;
using CustomPresenter.Domain.Live;
using CustomPresenter.Domain.Media;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CustomPresenter.Infrastructure.ObsServer;

/// <summary>
/// Micro-servidor HTTP + WebSocket enlazado estrictamente a 127.0.0.1 para OBS Browser Source y Operador.
/// </summary>
public sealed class ObsWebServer : IAsyncDisposable
{
    private const string OperatorClientHeader = "X-CustomPresenter-Client";
    private const string OperatorClientValue = "operator";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ObsServerOptions _options;
    private readonly LiveStateManager _liveStateManager;
    private readonly ChannelLiveBroadcaster _broadcaster;
    private readonly IMediaRepository? _mediaRepository;
    private readonly IMediaStorage? _mediaStorage;
    private readonly PresenterServices? _services;
    private readonly CancellationTokenSource _shutdownCts = new();
    private WebApplication? _app;
    private int _disposed;

    public ObsWebServer(
        ObsServerOptions options,
        LiveStateManager liveStateManager,
        ChannelLiveBroadcaster broadcaster,
        IMediaRepository? mediaRepository = null,
        IMediaStorage? mediaStorage = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(liveStateManager);
        ArgumentNullException.ThrowIfNull(broadcaster);

        _options = options;
        _liveStateManager = liveStateManager;
        _broadcaster = broadcaster;
        _mediaRepository = mediaRepository;
        _mediaStorage = mediaStorage;
    }

    public ObsWebServer(
        ObsServerOptions options,
        LiveStateManager liveStateManager,
        ChannelLiveBroadcaster broadcaster,
        PresenterServices services)
        : this(
            options,
            liveStateManager,
            broadcaster,
            services?.MediaRepository,
            services?.MediaStorage)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
    }

    public IPEndPoint BoundEndpoint { get; private set; } = new(IPAddress.Loopback, 0);

    public Uri BaseUri => new($"http://{BoundEndpoint.Address}:{BoundEndpoint.Port}");

    /// <summary>
    /// Inicia el servidor Kestrel únicamente sobre 127.0.0.1.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_app is not null)
        {
            return;
        }

        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.Listen(_options.BindAddress, _options.Port);
        });

        var app = builder.Build();
        app.UseWebSockets();

        app.Use(async (context, next) =>
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Content-Security-Policy"] =
                "default-src 'none'; script-src 'self'; style-src 'self'; img-src 'self'; media-src 'self'; connect-src 'self' ws://127.0.0.1:*; frame-ancestors 'none'";
            await next(context).ConfigureAwait(false);
        });

        app.MapGet("/obs", () => Results.Content(ObsStaticAssets.IndexHtml, "text/html; charset=utf-8"));
        app.MapGet("/obs/app.css", () => Results.Content(ObsStaticAssets.AppCss, "text/css; charset=utf-8"));
        app.MapGet("/obs/app.js", () => Results.Content(ObsStaticAssets.AppJs, "application/javascript; charset=utf-8"));
        app.MapGet("/ws", HandleWebSocketAsync);

        app.MapGet("/api/live", () => Results.Json(_liveStateManager.Snapshot(), JsonOptions));
        app.MapPost("/api/live/project", (RequestDelegate)HandleProjectSlideAsync);
        MapOperatorPost(app, "/api/live/clear", ct => _liveStateManager.ToggleClearTextAsync(ct));
        MapOperatorPost(app, "/api/live/blackout", ct => _liveStateManager.ToggleBlackoutAsync(ct));
        MapOperatorPost(app, "/api/live/logo", ct => _liveStateManager.ToggleLogoAsync(ct));
        app.MapGet("/media/{id:guid}", HandleGetMediaAsync);

        if (_services is not null)
        {
            ObsApiEndpoints.MapPresenterEndpoints(
                app,
                _services,
                _liveStateManager,
                JsonOptions,
                IsAuthorizedOperatorPost);
        }

        await app.StartAsync(cancellationToken).ConfigureAwait(false);
        _app = app;

        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>();
        var firstAddress = addresses?.Addresses.FirstOrDefault();
        if (firstAddress is not null && Uri.TryCreate(firstAddress, UriKind.Absolute, out var uri))
        {
            BoundEndpoint = new IPEndPoint(IPAddress.Loopback, uri.Port);
        }
    }

    /// <summary>
    /// Detiene el servidor y cierra los sockets activos limpiamente.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_app is null)
        {
            return;
        }

        await _shutdownCts.CancelAsync().ConfigureAwait(false);
        await _app.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            await StopAsync().ConfigureAwait(false);
        }
        finally
        {
            if (_app is not null)
            {
                await _app.DisposeAsync().ConfigureAwait(false);
                _app = null;
            }

            _shutdownCts.Dispose();
        }
    }

    private static bool IsAuthorizedOperatorPost(HttpRequest request)
    {
        if (!request.Headers.TryGetValue(OperatorClientHeader, out var clientHeader)
            || !string.Equals(clientHeader.ToString(), OperatorClientValue, StringComparison.Ordinal))
        {
            return false;
        }

        if (request.Headers.TryGetValue("Origin", out var originHeader))
        {
            var originRaw = originHeader.ToString();
            if (!Uri.TryCreate(originRaw, UriKind.Absolute, out var originUri))
            {
                return false;
            }

            if (!string.Equals(originUri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(originUri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static void MapOperatorPost(
        WebApplication app,
        string pattern,
        Func<CancellationToken, Task<LiveState>> action)
    {
        async Task Handler(HttpContext ctx) =>
            await HandleAuthorizedPostAsync(ctx, () => action(ctx.RequestAborted)).ConfigureAwait(false);

        app.MapPost(pattern, (RequestDelegate)Handler);
    }

    private static async Task HandleAuthorizedPostAsync(
        HttpContext context,
        Func<Task<LiveState>> action)
    {
        if (!IsAuthorizedOperatorPost(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var state = await action().ConfigureAwait(false);
        await Results.Json(state, JsonOptions).ExecuteAsync(context).ConfigureAwait(false);
    }

    private async Task HandleProjectSlideAsync(HttpContext context)
    {
        if (!IsAuthorizedOperatorPost(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        SlideContent? slide;
        try
        {
            slide = await JsonSerializer.DeserializeAsync<SlideContent>(
                context.Request.Body,
                JsonOptions,
                context.RequestAborted).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            await Results.BadRequest("Payload JSON inválido.").ExecuteAsync(context).ConfigureAwait(false);
            return;
        }

        if (slide is null)
        {
            await Results.BadRequest("Se requiere una diapositiva válida.").ExecuteAsync(context).ConfigureAwait(false);
            return;
        }

        try
        {
            var updated = await _liveStateManager.ProjectSlideAsync(slide, context.RequestAborted)
                .ConfigureAwait(false);
            await Results.Json(updated, JsonOptions).ExecuteAsync(context).ConfigureAwait(false);
        }
        catch (DomainException ex)
        {
            await Results.BadRequest(ex.Message).ExecuteAsync(context).ConfigureAwait(false);
        }
    }

    private async Task HandleWebSocketAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("Se requiere una conexión WebSocket válida.").ConfigureAwait(false);
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
        using var subscription = _broadcaster.Subscribe();
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            context.RequestAborted,
            _shutdownCts.Token);
        var ct = linkedCts.Token;

        try
        {
            await SendStateAsync(socket, _liveStateManager.Snapshot(), ct).ConfigureAwait(false);

            var receiveTask = DrainClientFramesAsync(socket, linkedCts);
            await foreach (var state in subscription.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                if (socket.State != WebSocketState.Open)
                {
                    break;
                }

                await SendStateAsync(socket, state, ct).ConfigureAwait(false);
            }

            await receiveTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Apagado del servidor o desconexión solicitada.
        }
        catch (WebSocketException)
        {
            // El cliente OBS cerró abruptamente el socket.
        }
        finally
        {
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                try
                {
                    await socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Cierre de servidor",
                        CancellationToken.None).ConfigureAwait(false);
                }
                catch (WebSocketException)
                {
                    // El socket ya fue abortado por el cliente.
                }
                catch (ObjectDisposedException)
                {
                    // El socket ya fue liberado.
                }
            }
        }
    }

    private static async Task SendStateAsync(
        WebSocket socket,
        LiveState state,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(state, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
    }

    private static async Task DrainClientFramesAsync(WebSocket socket, CancellationTokenSource linkedCts)
    {
        var buffer = new byte[256];
        try
        {
            while (socket.State == WebSocketState.Open && !linkedCts.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(buffer, linkedCts.Token).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await linkedCts.CancelAsync().ConfigureAwait(false);
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelado durante el cierre.
        }
        catch (WebSocketException)
        {
            await linkedCts.CancelAsync().ConfigureAwait(false);
        }
    }

    private async Task<IResult> HandleGetMediaAsync(Guid id, CancellationToken cancellationToken)
    {
        if (_mediaRepository is null || _mediaStorage is null)
        {
            return Results.NotFound();
        }

        var asset = await _mediaRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (asset is null)
        {
            return Results.NotFound();
        }

        try
        {
            var safePath = _mediaStorage.ResolveSafeFilePath(asset);
            return Results.File(safePath, asset.ContentType, enableRangeProcessing: true);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DomainException)
        {
            return Results.NotFound();
        }
    }
}
