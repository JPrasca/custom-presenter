using System.Globalization;
using CustomPresenter.Domain.Live;

namespace CustomPresenter.Domain.Playlists;

/// <summary>
/// Tipo de contenido heterogéneo referenciado por un ítem de la lista de servicio.
/// </summary>
public enum PlaylistItemKind
{
    Song = 1,
    BibleVerse = 2,
    Announcement = 3,
}

/// <summary>
/// Elemento individual ordenado dentro de una lista de servicio (playlist).
/// </summary>
public sealed record PlaylistItem(
    Guid Id,
    int Position,
    PlaylistItemKind Kind,
    string ReferenceKey,
    string DisplayTitle,
    string? Subtitle = null);

/// <summary>
/// Lista de servicio mixta y ordenada con canciones, pasajes bíblicos y anuncios.
/// </summary>
public sealed record Playlist(
    Guid Id,
    string Name,
    IReadOnlyList<PlaylistItem> Items,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    public const int MaxNameLength = 200;
    public const int MaxItems = 500;

    public static Playlist Create(string name)
    {
        var normalizedName = NormalizeAndValidateName(name);
        var now = DateTimeOffset.UtcNow;

        return new Playlist(
            Guid.NewGuid(),
            normalizedName,
            [],
            now,
            now);
    }

    public Playlist Rename(string name)
    {
        var normalizedName = NormalizeAndValidateName(name);
        return this with
        {
            Name = normalizedName,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    public Playlist WithAddedItem(
        PlaylistItemKind kind,
        string referenceKey,
        string displayTitle,
        string? subtitle = null)
    {
        if (Items.Count >= MaxItems)
        {
            throw new DomainException($"La playlist no puede superar los {MaxItems} ítems.");
        }

        if (string.IsNullOrWhiteSpace(referenceKey))
        {
            throw new DomainException("La referencia del ítem de playlist no puede estar vacía.");
        }

        if (string.IsNullOrWhiteSpace(displayTitle))
        {
            throw new DomainException("El título visible del ítem de playlist no puede estar vacío.");
        }

        var nextItems = new List<PlaylistItem>(Items.Count + 1);
        nextItems.AddRange(Items);
        nextItems.Add(new PlaylistItem(
            Guid.NewGuid(),
            Items.Count,
            kind,
            referenceKey.Trim(),
            displayTitle.Trim(),
            string.IsNullOrWhiteSpace(subtitle) ? null : subtitle.Trim()));

        return this with
        {
            Items = nextItems,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    public Playlist WithRemovedItem(Guid itemId)
    {
        var remaining = Items.Where(item => item.Id != itemId).ToList();
        if (remaining.Count == Items.Count)
        {
            throw new DomainException($"No se encontró el ítem '{itemId}' en la playlist '{Name}'.");
        }

        var normalized = remaining
            .Select((item, index) => item with { Position = index })
            .ToList();

        return this with
        {
            Items = normalized,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>
    /// Reordena los ítems validando que <paramref name="orderedItemIds"/> sea una permutación exacta.
    /// </summary>
    public Playlist WithReorderedItems(IReadOnlyList<Guid> orderedItemIds)
    {
        ArgumentNullException.ThrowIfNull(orderedItemIds);

        if (orderedItemIds.Count != Items.Count)
        {
            throw new DomainException(
                "El reordenamiento debe incluir exactamente todos los ítems actuales de la playlist.");
        }

        var byId = Items.ToDictionary(item => item.Id);
        var seen = new HashSet<Guid>();
        var reordered = new List<PlaylistItem>(Items.Count);

        for (var i = 0; i < orderedItemIds.Count; i++)
        {
            var id = orderedItemIds[i];
            if (!seen.Add(id))
            {
                throw new DomainException($"El identificador '{id}' aparece duplicado en el reordenamiento.");
            }

            if (!byId.TryGetValue(id, out var existing))
            {
                throw new DomainException($"El identificador '{id}' no pertenece a la playlist '{Name}'.");
            }

            reordered.Add(existing with { Position = i });
        }

        return this with
        {
            Items = reordered,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    public static string FormatBibleReferenceKey(
        Guid versionId,
        int bookNumber,
        int chapter,
        int startVerse,
        int? endVerse = null)
    {
        var end = endVerse ?? startVerse;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{versionId:D}|{bookNumber}|{chapter}|{startVerse}|{end}");
    }

    public static bool TryParseBibleReferenceKey(
        string referenceKey,
        out Guid versionId,
        out int bookNumber,
        out int chapter,
        out int startVerse,
        out int endVerse)
    {
        versionId = Guid.Empty;
        bookNumber = 0;
        chapter = 0;
        startVerse = 0;
        endVerse = 0;

        if (string.IsNullOrWhiteSpace(referenceKey))
        {
            return false;
        }

        var parts = referenceKey.Split('|');
        if (parts.Length != 5)
        {
            return false;
        }

        return Guid.TryParse(parts[0], out versionId)
            && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out bookNumber)
            && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out chapter)
            && int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out startVerse)
            && int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out endVerse)
            && bookNumber > 0
            && chapter > 0
            && startVerse > 0
            && endVerse >= startVerse;
    }

    private static string NormalizeAndValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("El nombre de la playlist no puede estar vacío.");
        }

        var trimmed = name.Trim();
        if (trimmed.Length > MaxNameLength)
        {
            throw new DomainException(
                $"El nombre de la playlist excede el máximo de {MaxNameLength} caracteres.");
        }

        return trimmed;
    }
}
