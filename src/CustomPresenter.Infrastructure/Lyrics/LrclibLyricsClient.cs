using System.Text;
using System.Text.Json;
using CustomPresenter.Domain.Live;
using CustomPresenter.Domain.Songs;

namespace CustomPresenter.Infrastructure.Lyrics;

/// <summary>
/// Cliente HTTP seguro para consultar letras públicas en LRCLIB con timeout, límite de tamaño y fallback offline.
/// </summary>
public sealed class LrclibLyricsClient : ILyricsLookupClient
{
    public const int MaxResponseBytes = 2 * 1024 * 1024;
    private static readonly Uri BaseSearchUri = new("https://lrclib.net/api/search");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        MaxDepth = 8,
    };

    private readonly HttpClient _httpClient;

    public LrclibLyricsClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
        if (_httpClient.Timeout == Timeout.InfiniteTimeSpan || _httpClient.Timeout > TimeSpan.FromSeconds(10))
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(5);
        }
    }

    public async Task<IReadOnlyList<LyricsSearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new DomainException("La consulta de búsqueda de letras no puede estar vacía.");
        }

        var requestUri = new Uri($"{BaseSearchUri}?q={Uri.EscapeDataString(query.Trim())}");
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.UserAgent.ParseAdd("CustomPresenter/1.0");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new DomainException(
                "No se pudo conectar con el servicio de letras en internet. Puedes continuar trabajando en modo offline.",
                ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new DomainException(
                    $"El servicio de letras en internet respondió con estado {(int)response.StatusCode}.");
            }

            var json = await ReadBoundedUtf8Async(response.Content, cancellationToken).ConfigureAwait(false);
            List<LrclibDto>? items;
            try
            {
                items = JsonSerializer.Deserialize<List<LrclibDto>>(json, JsonOptions);
            }
            catch (JsonException ex)
            {
                throw new DomainException("La respuesta del servicio de letras tiene un formato inválido.", ex);
            }

            if (items is null || items.Count == 0)
            {
                return [];
            }

            return items
                .Where(item => !string.IsNullOrWhiteSpace(item.PlainLyrics) || !string.IsNullOrWhiteSpace(item.SyncedLyrics))
                .Take(10)
                .Select(item =>
                {
                    var raw = !string.IsNullOrWhiteSpace(item.PlainLyrics)
                        ? item.PlainLyrics!
                        : item.SyncedLyrics!;
                    var formatted = SongLyricsFormatter.Format(raw);
                    return new LyricsSearchResult(
                        string.IsNullOrWhiteSpace(item.TrackName) ? "Sin título" : item.TrackName.Trim(),
                        string.IsNullOrWhiteSpace(item.ArtistName) ? string.Empty : item.ArtistName.Trim(),
                        formatted);
                })
                .Where(r => r.FormattedLyrics.Length > 0)
                .ToList();
        }
    }

    private static async Task<string> ReadBoundedUtf8Async(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var ms = new MemoryStream();
        var buffer = new byte[8192];
        var total = 0;

        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            total += read;
            if (total > MaxResponseBytes)
            {
                throw new DomainException("La respuesta del servicio de letras excede el tamaño máximo permitido.");
            }

            await ms.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        return Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
    }

    private sealed record LrclibDto(
        string? TrackName,
        string? ArtistName,
        string? PlainLyrics,
        string? SyncedLyrics);
}
