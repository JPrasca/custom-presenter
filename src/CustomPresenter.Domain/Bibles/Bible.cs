using CustomPresenter.Domain.Live;

namespace CustomPresenter.Domain.Bibles;

/// <summary>
/// Metadatos de una versión bíblica importada localmente.
/// </summary>
public sealed record BibleVersion(
    Guid Id,
    string Name,
    string Abbreviation,
    DateTimeOffset ImportedAtUtc)
{
    public static BibleVersion Create(string name, string abbreviation)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("El nombre de la versión bíblica no puede estar vacío.");
        }

        if (string.IsNullOrWhiteSpace(abbreviation))
        {
            throw new DomainException("La abreviatura de la versión bíblica no puede estar vacía.");
        }

        return new BibleVersion(
            Guid.NewGuid(),
            name.Trim(),
            abbreviation.Trim().ToUpperInvariant(),
            DateTimeOffset.UtcNow);
    }
}

/// <summary>
/// Versículo bíblico individual perteneciente a una versión.
/// </summary>
public sealed record BibleVerse(
    Guid VersionId,
    int BookNumber,
    string BookName,
    int Chapter,
    int Verse,
    string Text)
{
    public void Validate()
    {
        if (BookNumber <= 0 || Chapter <= 0 || Verse <= 0)
        {
            throw new DomainException("El libro, capítulo y versículo deben ser mayores a cero.");
        }

        if (string.IsNullOrWhiteSpace(BookName) || string.IsNullOrWhiteSpace(Text))
        {
            throw new DomainException("El nombre del libro y el texto del versículo son obligatorios.");
        }
    }
}

/// <summary>
/// Resumen de un libro bíblico disponible en una versión.
/// </summary>
public sealed record BibleBookSummary(
    int BookNumber,
    string BookName,
    int ChaptersCount);

/// <summary>
/// Resultado de una importación de Biblia validada en memoria.
/// </summary>
public sealed record BibleImportResult(
    BibleVersion Version,
    IReadOnlyList<BibleVerse> Verses);
