using System.Globalization;
using System.Text;
using System.Text.Json;
using CustomPresenter.Domain.Bibles;
using CustomPresenter.Domain.Live;

namespace CustomPresenter.Infrastructure.Http;

/// <summary>
/// Cliente HTTP seguro para consultar pasajes y capítulos bíblicos a demanda desde internet con timeout y protección de memoria.
/// </summary>
public sealed class BibleOnlineClient : IBibleOnlineProvider
{
    public const int MaxResponseBytes = 512 * 1024;
    private static readonly Uri BaseApiUri = new("https://bible-api.com/");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        MaxDepth = 8,
    };

    private readonly HttpClient _httpClient;

    public BibleOnlineClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
        if (_httpClient.Timeout == Timeout.InfiniteTimeSpan || _httpClient.Timeout > TimeSpan.FromSeconds(10))
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(5);
        }
    }

    public async Task<IReadOnlyList<BibleVerse>> FetchChapterVersesAsync(
        Guid versionId,
        string versionAbbreviation,
        int bookNumber,
        string bookName,
        int chapter,
        CancellationToken cancellationToken = default)
    {
        if (chapter <= 0)
        {
            return [];
        }

        var translationParam = ResolveTranslationParam(versionAbbreviation);
        var canonicalName = BibleReferenceParser.GetCanonicalBookName(bookNumber) ?? bookName;
        var queryPath = $"{Uri.EscapeDataString(canonicalName)}+{chapter}?translation={translationParam}";
        var requestUri = new Uri(BaseApiUri, queryPath);

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
            // Fallback silencioso en modo offline sin romper el flujo de la aplicación
            return [];
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            var json = await ReadBoundedUtf8Async(response.Content, cancellationToken).ConfigureAwait(false);
            BibleApiResponseDto? dto;
            try
            {
                dto = JsonSerializer.Deserialize<BibleApiResponseDto>(json, JsonOptions);
            }
            catch (JsonException)
            {
                return [];
            }

            if (dto?.Verses is null || dto.Verses.Count == 0)
            {
                return [];
            }

            var verses = new List<BibleVerse>(dto.Verses.Count);
            foreach (var v in dto.Verses)
            {
                if (v.Verse <= 0 || string.IsNullOrWhiteSpace(v.Text))
                {
                    continue;
                }

                var cleanText = v.Text.Trim();
                var verse = new BibleVerse(
                    versionId,
                    bookNumber,
                    canonicalName,
                    chapter,
                    v.Verse,
                    cleanText);
                verse.Validate();
                verses.Add(verse);
            }

            return verses;
        }
    }

    private static string ResolveTranslationParam(string abbreviation)
    {
        var norm = (abbreviation ?? string.Empty).Trim().ToLowerInvariant();
        if (norm.Contains("1909", StringComparison.Ordinal))
        {
            return "rv1909";
        }

        // Por defecto en español en APIs públicas usamos rv1909 (Reina Valera de libre acceso)
        return "rv1909";
    }

    private static async Task<string> ReadBoundedUtf8Async(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var buffer = new byte[8192];
        var totalBytes = 0;
        using var ms = new MemoryStream();

        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            totalBytes += read;
            if (totalBytes > MaxResponseBytes)
            {
                throw new DomainException("La respuesta del servicio bíblico excedió el límite de seguridad (512 KB).");
            }

            ms.Write(buffer, 0, read);
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private sealed record BibleApiResponseDto(
        string? Reference,
        List<BibleApiVerseDto>? Verses);

    private sealed record BibleApiVerseDto(
        int Chapter,
        int Verse,
        string? Text);
}
