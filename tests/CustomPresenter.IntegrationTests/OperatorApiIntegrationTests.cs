using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using CustomPresenter.Application.Display;
using CustomPresenter.Domain.Display;
using CustomPresenter.Domain.Live;
using CustomPresenter.IntegrationTests.Builders;
using Xunit;

namespace CustomPresenter.IntegrationTests;

public sealed class OperatorApiIntegrationTests
{
    private sealed class SpyProjectorWindowManager : IProjectorWindowManager
    {
        private readonly List<MonitorInfo> _monitors =
        [
            new("mon-1", "Principal", 0, 0, 1920, 1080, true),
        ];

        public bool AllowsWriteIpcHandlers => false;
        public bool IsOpen { get; private set; }
        public MonitorInfo? ActiveMonitor { get; private set; }
        public List<LiveState> ReceivedStates { get; } = [];

        public IReadOnlyList<MonitorInfo> GetMonitors() => _monitors;

        public Task OpenAsync(
            MonitorInfo targetMonitor,
            ProjectorDisplayMode displayMode,
            CancellationToken cancellationToken = default)
        {
            IsOpen = true;
            ActiveMonitor = targetMonitor;
            return Task.CompletedTask;
        }

        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            IsOpen = false;
            ActiveMonitor = null;
            return Task.CompletedTask;
        }

        public ValueTask SendLiveStateAsync(
            LiveState state,
            CancellationToken cancellationToken = default)
        {
            if (IsOpen)
            {
                ReceivedStates.Add(state);
            }

            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task PostApiLiveSinCabeceraClienteOConOriginExternoDevuelve403Forbidden()
    {
        // Arrange
        var (server, _, _) = new ObsServerTestBuilder().Build();
        await server.StartAsync();
        await using var serverScope = server;
        using var http = new HttpClient { BaseAddress = server.BaseUri };

        // Act - sin cabecera X-CustomPresenter-Client
        var missingHeader = await http.PostAsync("/api/live/blackout", null);

        // Act - con Origin externo malicioso incluso si intenta enviar cabecera
        using var evilReq = new HttpRequestMessage(HttpMethod.Post, "/api/live/blackout");
        evilReq.Headers.Add("X-CustomPresenter-Client", "operator");
        evilReq.Headers.Add("Origin", "https://evil.example");
        var externalOrigin = await http.SendAsync(evilReq);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, missingHeader.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, externalOrigin.StatusCode);
    }

    [Fact]
    public async Task ComandosOperadorActualizanLiveStateYDifundenAProyectorYWebSocketObs()
    {
        // Arrange
        var (server, manager, broadcaster) = new ObsServerTestBuilder().Build();
        var projectorWindow = new SpyProjectorWindowManager();
        using var projectorService = new ProjectorService(projectorWindow, manager, broadcaster);
        await server.StartAsync();
        await using var serverScope = server;
        await projectorService.OpenOnMonitorAsync("mon-1");

        using var http = new HttpClient { BaseAddress = server.BaseUri };
        http.DefaultRequestHeaders.Add("X-CustomPresenter-Client", "operator");

        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{server.BoundEndpoint.Port}/ws"), CancellationToken.None);
        _ = await ReceiveTextAsync(ws); // snapshot inicial

        var slide = new SlideContentBuilder().WithText("Santo, Santo, Santo").Build();

        // Act
        var projectRes = await http.PostAsJsonAsync("/api/live/project", slide);
        var obsProjectJson = await ReceiveTextAsync(ws);
        var clearRes = await http.PostAsync("/api/live/clear", null);
        var obsClearJson = await ReceiveTextAsync(ws);
        var blackoutRes = await http.PostAsync("/api/live/blackout", null);
        var obsBlackoutJson = await ReceiveTextAsync(ws);
        var logoRes = await http.PostAsync("/api/live/logo", null);
        var obsLogoJson = await ReceiveTextAsync(ws);

        await WaitUntilAsync(() => projectorWindow.ReceivedStates.Count >= 5);

        // Assert
        Assert.Equal(HttpStatusCode.OK, projectRes.StatusCode);
        Assert.Equal(HttpStatusCode.OK, clearRes.StatusCode);
        Assert.Equal(HttpStatusCode.OK, blackoutRes.StatusCode);
        Assert.Equal(HttpStatusCode.OK, logoRes.StatusCode);

        using var obsProjectDoc = JsonDocument.Parse(obsProjectJson);
        using var obsClearDoc = JsonDocument.Parse(obsClearJson);
        using var obsBlackoutDoc = JsonDocument.Parse(obsBlackoutJson);
        using var obsLogoDoc = JsonDocument.Parse(obsLogoJson);

        Assert.True(obsProjectDoc.RootElement.GetProperty("isTextVisible").GetBoolean());
        Assert.False(obsClearDoc.RootElement.GetProperty("isTextVisible").GetBoolean());
        Assert.True(obsBlackoutDoc.RootElement.GetProperty("flags").GetProperty("blackout").GetBoolean());
        Assert.True(obsLogoDoc.RootElement.GetProperty("flags").GetProperty("logo").GetBoolean());

        Assert.True(projectorWindow.ReceivedStates.Count >= 5);
        Assert.Equal(EffectiveDisplayMode.Slide, projectorWindow.ReceivedStates[1].EffectiveMode);
        Assert.Equal(EffectiveDisplayMode.ClearText, projectorWindow.ReceivedStates[2].EffectiveMode);
        Assert.Equal(EffectiveDisplayMode.Blackout, projectorWindow.ReceivedStates[3].EffectiveMode);
    }

    [Fact]
    public async Task PostApiLiveProjectConTextoVacioDevuelve400BadRequest()
    {
        // Arrange
        var (server, _, _) = new ObsServerTestBuilder().Build();
        await server.StartAsync();
        await using var serverScope = server;
        using var http = new HttpClient { BaseAddress = server.BaseUri };
        http.DefaultRequestHeaders.Add("X-CustomPresenter-Client", "operator");
        var invalidSlide = new SlideContentBuilder().WithText("   ").Build();

        // Act
        var response = await http.PostAsJsonAsync("/api/live/project", invalidSlide);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<string> ReceiveTextAsync(ClientWebSocket ws)
    {
        var buffer = new byte[8192];
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var result = await ws.ReceiveAsync(buffer, cts.Token);
        return Encoding.UTF8.GetString(buffer, 0, result.Count);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!predicate() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
    }
}
