using System.Net;
using System.Text;
using CustomPresenter.Domain.Live;
using CustomPresenter.Infrastructure.Http;
using Xunit;

namespace CustomPresenter.IntegrationTests;

public sealed class BibleOnlineClientTests
{
    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            return Task.FromResult(handler(request));
        }
    }

    [Fact]
    public async Task FetchChapterVersesAsyncConRespuestaValidaParseaVersiculosConCitaYTexto()
    {
        // Arrange
        const string json = """
            {
              "reference": "Salmos 23",
              "verses": [
                {
                  "book_id": "PSA",
                  "book_name": "Salmos",
                  "chapter": 23,
                  "verse": 1,
                  "text": "Jehová es mi pastor; nada me faltará."
                },
                {
                  "book_id": "PSA",
                  "book_name": "Salmos",
                  "chapter": 23,
                  "verse": 2,
                  "text": "En lugares de delicados pastos me hará descansar."
                }
              ]
            }
            """;

        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });

        using var http = new HttpClient(handler);
        var client = new BibleOnlineClient(http);
        var versionId = Guid.NewGuid();

        // Act
        var verses = await client.FetchChapterVersesAsync(versionId, "RVR60", 19, "Salmos", 23);

        // Assert
        Assert.NotNull(handler.LastRequestUri);
        Assert.Contains("Salmos+23", handler.LastRequestUri.AbsoluteUri, StringComparison.Ordinal);
        Assert.Equal(2, verses.Count);
        Assert.Equal(versionId, verses[0].VersionId);
        Assert.Equal(19, verses[0].BookNumber);
        Assert.Equal("Salmos", verses[0].BookName);
        Assert.Equal(23, verses[0].Chapter);
        Assert.Equal(1, verses[0].Verse);
        Assert.Equal("Jehová es mi pastor; nada me faltará.", verses[0].Text);
    }

    [Fact]
    public async Task FetchChapterVersesAsyncSinConexionRetornaListaVaciaSinLanzarExcepcion()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("Sin red"));
        using var http = new HttpClient(handler);
        var client = new BibleOnlineClient(http);

        // Act
        var verses = await client.FetchChapterVersesAsync(Guid.NewGuid(), "RVR60", 19, "Salmos", 23);

        // Assert
        Assert.Empty(verses);
    }

    [Fact]
    public async Task FetchChapterVersesAsyncConRespuestaMayorALimiteLanzaDomainException()
    {
        // Arrange
        var oversized = new string('x', BibleOnlineClient.MaxResponseBytes + 1024);
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(oversized, Encoding.UTF8, "text/plain"),
        });
        using var http = new HttpClient(handler);
        var client = new BibleOnlineClient(http);

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(
            () => client.FetchChapterVersesAsync(Guid.NewGuid(), "RVR60", 19, "Salmos", 23));
    }
}
