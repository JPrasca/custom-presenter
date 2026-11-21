using System.Net;
using System.Text;
using CustomPresenter.Domain.Live;
using CustomPresenter.Infrastructure.Lyrics;
using Xunit;

namespace CustomPresenter.IntegrationTests;

public sealed class LrclibLyricsClientTests
{
    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            return Task.FromResult(_handler(request));
        }
    }

    [Fact]
    public async Task SearchAsyncEscapaQueryYFormateaResultadosDeLrclib()
    {
        // Arrange
        const string json = """
            [
              {
                "trackName": "Sublime Gracia",
                "artistName": "John Newton",
                "plainLyrics": "Línea 1\nLínea 2\nLínea 3\nLínea 4\nLínea 5"
              }
            ]
            """;

        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });

        using var http = new HttpClient(handler);
        var client = new LrclibLyricsClient(http);

        // Act
        var results = await client.SearchAsync("Sublime Gracia & Paz");

        // Assert
        Assert.NotNull(handler.LastRequestUri);
        Assert.Contains("Sublime%20Gracia%20%26%20Paz", handler.LastRequestUri.AbsoluteUri, StringComparison.Ordinal);
        Assert.Single(results);
        Assert.Equal("Sublime Gracia", results[0].TrackName);
        Assert.Equal("John Newton", results[0].ArtistName);
        Assert.Contains("\n\nLínea 5", results[0].FormattedLyrics, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchAsyncSinInternetLanzaDomainExceptionControlada()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("Sin conexión"));
        using var http = new HttpClient(handler);
        var client = new LrclibLyricsClient(http);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainException>(() => client.SearchAsync("Sublime Gracia"));
        Assert.Contains("internet", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
