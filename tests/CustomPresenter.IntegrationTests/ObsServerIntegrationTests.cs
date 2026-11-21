using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using CustomPresenter.IntegrationTests.Builders;
using Xunit;

namespace CustomPresenter.IntegrationTests;

public sealed class ObsServerIntegrationTests
{
    [Fact]
    public async Task GetObsDevuelveHtmlConCspEstrictaYFondoTransparente()
    {
        // Arrange
        var (server, _, _) = new ObsServerTestBuilder().Build();
        await server.StartAsync();
        await using var serverScope = server;
        using var http = new HttpClient { BaseAddress = server.BaseUri };

        // Act
        var htmlResponse = await http.GetAsync("/obs");
        var html = await htmlResponse.Content.ReadAsStringAsync();
        var cssResponse = await http.GetAsync("/obs/app.css");
        var css = await cssResponse.Content.ReadAsStringAsync();
        var jsResponse = await http.GetAsync("/obs/app.js");
        var js = await jsResponse.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(IPAddress.Loopback, server.BoundEndpoint.Address);
        Assert.Equal(HttpStatusCode.OK, htmlResponse.StatusCode);
        Assert.True(htmlResponse.Headers.TryGetValues("Content-Security-Policy", out var cspValues));
        var csp = string.Join(";", cspValues);
        Assert.DoesNotContain("'unsafe-inline'", csp, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nosniff", htmlResponse.Headers.GetValues("X-Content-Type-Options"));

        Assert.Equal(HttpStatusCode.OK, cssResponse.StatusCode);
        Assert.Contains("background: transparent", css, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(HttpStatusCode.OK, jsResponse.StatusCode);
        Assert.Contains("textContent", js, StringComparison.Ordinal);
        Assert.DoesNotContain("innerHTML", js, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/ws", js, StringComparison.Ordinal);
        Assert.Contains("/obs/app.js", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetWsSinUpgradeWebSocketDevuelveBadRequest400()
    {
        // Arrange
        var (server, _, _) = new ObsServerTestBuilder().Build();
        await server.StartAsync();
        await using var serverScope = server;
        using var http = new HttpClient { BaseAddress = server.BaseUri };

        // Act
        var response = await http.GetAsync("/ws");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task WebSocketEnviaEstadoInicialYActualizacionesEnMenosDe50Ms()
    {
        // Arrange
        var (server, manager, _) = new ObsServerTestBuilder().Build();
        await server.StartAsync();
        await using var serverScope = server;
        using var ws = new ClientWebSocket();
        var wsUri = new Uri($"ws://127.0.0.1:{server.BoundEndpoint.Port}/ws");
        await ws.ConnectAsync(wsUri, CancellationToken.None);

        // Act - snapshot inicial
        var initialJson = await ReceiveTextAsync(ws);
        using var initialDoc = JsonDocument.Parse(initialJson);

        // Act - proyectar diapositiva y medir latencia
        var slide = new SlideContentBuilder()
            .WithText("<script>alert(1)</script> & \"prueba\"")
            .Build();
        var sw = Stopwatch.StartNew();
        await manager.ProjectSlideAsync(slide);
        var updateJson = await ReceiveTextAsync(ws);
        sw.Stop();
        using var updateDoc = JsonDocument.Parse(updateJson);

        // Assert
        Assert.True(initialDoc.RootElement.TryGetProperty("flags", out _));
        Assert.Equal(
            "<script>alert(1)</script> & \"prueba\"",
            updateDoc.RootElement.GetProperty("currentSlide").GetProperty("text").GetString());
        Assert.True(updateDoc.RootElement.GetProperty("isTextVisible").GetBoolean());
        Assert.True(sw.ElapsedMilliseconds < 50, $"Latencia excesiva: {sw.ElapsedMilliseconds} ms");
    }

    [Fact]
    public async Task ApagadoLimpioConClienteDesconectadoAbruptamenteNoLanzaExcepcion()
    {
        // Arrange
        var (server, _, _) = new ObsServerTestBuilder().Build();
        await server.StartAsync();
        var ws = new ClientWebSocket();
        var wsUri = new Uri($"ws://127.0.0.1:{server.BoundEndpoint.Port}/ws");
        await ws.ConnectAsync(wsUri, CancellationToken.None);

        // Act - simular desconexión abrupta de OBS y apagado inmediato del servidor
        ws.Abort();
        ws.Dispose();
        await server.StopAsync();
        await server.DisposeAsync();

        // Assert - si llega aquí sin excepciones, el apagado fue limpio
        Assert.True(true);
    }

    private static async Task<string> ReceiveTextAsync(ClientWebSocket ws)
    {
        var buffer = new byte[8192];
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var result = await ws.ReceiveAsync(buffer, cts.Token);
        return Encoding.UTF8.GetString(buffer, 0, result.Count);
    }
}
