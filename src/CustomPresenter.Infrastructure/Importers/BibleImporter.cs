using System.Globalization;
using System.Text.Json;
using System.Xml;
using CustomPresenter.Domain.Bibles;
using CustomPresenter.Domain.Live;

namespace CustomPresenter.Infrastructure.Importers;

/// <summary>
/// Importador seguro de Biblias (XML OpenSong/Zefania y JSON) protegido contra XXE y DoS.
/// </summary>
public sealed class BibleImporter : IBibleImporter
{
    public async Task<BibleImportResult> ImportAsync(
        Stream stream,
        string fileName,
        long reportedSizeBytes,
        string? versionNameOverride = null,
        string? abbreviationOverride = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new DomainException("Debe especificar el nombre del archivo de Biblia.");
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension is not ".xml" and not ".json")
        {
            throw new DomainException("Formato de Biblia no soportado. Use archivos .xml o .json.");
        }

        var content = await StreamReadHelper.ReadUtf8BoundedAsync(
            stream,
            reportedSizeBytes,
            BibleImportLimits.MaxFileSizeBytes,
            cancellationToken).ConfigureAwait(false);

        var defaultName = Path.GetFileNameWithoutExtension(fileName);

        return extension == ".json"
            ? ParseJson(content, versionNameOverride ?? defaultName, abbreviationOverride ?? defaultName)
            : ParseXml(content, versionNameOverride ?? defaultName, abbreviationOverride ?? defaultName);
    }

    private static BibleImportResult ParseXml(
        string content,
        string fallbackName,
        string fallbackAbbreviation)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersFromEntities = 0,
            MaxCharactersInDocument = BibleImportLimits.MaxXmlCharacters,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
        };

        try
        {
            using var stringReader = new StringReader(content);
            using var reader = XmlReader.Create(stringReader, settings);

            var versionName = fallbackName;
            var abbreviation = fallbackAbbreviation;
            var rawVerses = new List<(int BookNumber, string BookName, int Chapter, int Verse, string Text)>();

            var currentBookNumber = 0;
            var currentBookName = string.Empty;
            var currentChapter = 0;

            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                switch (reader.Name)
                {
                    case "bible":
                        versionName = reader.GetAttribute("name") ?? versionName;
                        abbreviation = reader.GetAttribute("abbreviation") ?? abbreviation;
                        break;

                    case "XMLBIBLE":
                        versionName = reader.GetAttribute("biblename") ?? versionName;
                        break;

                    case "identifier":
                        var idText = reader.ReadElementContentAsString().Trim();
                        if (idText.Length > 0)
                        {
                            abbreviation = idText;
                        }
                        break;

                    case "b":
                        currentBookName = (reader.GetAttribute("n") ?? string.Empty).Trim();
                        var canonicalNum = BibleReferenceParser.ResolveCanonicalBookNumber(currentBookName);
                        currentBookNumber = canonicalNum ?? (currentBookNumber + 1);
                        break;

                    case "BIBLEBOOK":
                        var bNumAttr = reader.GetAttribute("bnumber");
                        currentBookNumber = int.TryParse(bNumAttr, CultureInfo.InvariantCulture, out var parsedBook)
                            ? parsedBook
                            : currentBookNumber + 1;
                        currentBookName = (reader.GetAttribute("bname") ?? string.Empty).Trim();
                        break;

                    case "c":
                    case "CHAPTER":
                        var cAttr = reader.GetAttribute("n") ?? reader.GetAttribute("cnumber");
                        currentChapter = int.TryParse(cAttr, CultureInfo.InvariantCulture, out var parsedChap)
                            ? parsedChap
                            : 0;
                        break;

                    case "v":
                    case "VERS":
                        var vAttr = reader.GetAttribute("n") ?? reader.GetAttribute("vnumber");
                        var verseNum = int.TryParse(vAttr, CultureInfo.InvariantCulture, out var parsedVerse)
                            ? parsedVerse
                            : 0;
                        var verseText = reader.ReadElementContentAsString().Trim();
                        rawVerses.Add((currentBookNumber, currentBookName, currentChapter, verseNum, verseText));
                        break;
                }
            }

            return BuildValidatedResult(versionName, abbreviation, rawVerses);
        }
        catch (XmlException ex)
        {
            throw new DomainException($"Archivo XML inválido o rechazado por seguridad: {ex.Message}", ex);
        }
    }

    private static BibleImportResult ParseJson(
        string content,
        string fallbackName,
        string fallbackAbbreviation)
    {
        try
        {
            using var doc = JsonDocument.Parse(
                content,
                new JsonDocumentOptions { MaxDepth = BibleImportLimits.MaxJsonDepth });

            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new DomainException("El JSON de Biblia debe ser un objeto con 'verses'.");
            }

            var name = root.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String
                ? nameProp.GetString() ?? fallbackName
                : fallbackName;
            var abbreviation = root.TryGetProperty("abbreviation", out var abbrProp)
                && abbrProp.ValueKind == JsonValueKind.String
                ? abbrProp.GetString() ?? fallbackAbbreviation
                : fallbackAbbreviation;

            if (!root.TryGetProperty("verses", out var versesProp) || versesProp.ValueKind != JsonValueKind.Array)
            {
                throw new DomainException("El JSON de Biblia debe incluir un arreglo 'verses'.");
            }

            var rawVerses = new List<(int BookNumber, string BookName, int Chapter, int Verse, string Text)>();
            foreach (var item in versesProp.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    throw new DomainException("Cada versículo en JSON debe ser un objeto.");
                }

                var bookNumber = item.GetProperty("bookNumber").GetInt32();
                var bookName = item.GetProperty("bookName").GetString() ?? string.Empty;
                var chapter = item.GetProperty("chapter").GetInt32();
                var verse = item.GetProperty("verse").GetInt32();
                var text = item.GetProperty("text").GetString() ?? string.Empty;

                rawVerses.Add((bookNumber, bookName, chapter, verse, text));
            }

            return BuildValidatedResult(name, abbreviation, rawVerses);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new DomainException($"Formato JSON de Biblia inválido: {ex.Message}", ex);
        }
    }

    private static BibleImportResult BuildValidatedResult(
        string versionName,
        string abbreviation,
        List<(int BookNumber, string BookName, int Chapter, int Verse, string Text)> rawVerses)
    {
        if (rawVerses.Count == 0)
        {
            throw new DomainException("El archivo de Biblia no contiene versículos válidos.");
        }

        var version = BibleVersion.Create(versionName, abbreviation);
        var verses = new List<BibleVerse>(rawVerses.Count);

        foreach (var raw in rawVerses)
        {
            var verse = new BibleVerse(
                version.Id,
                raw.BookNumber,
                raw.BookName,
                raw.Chapter,
                raw.Verse,
                raw.Text);
            verse.Validate();
            verses.Add(verse);
        }

        return new BibleImportResult(version, verses);
    }
}
