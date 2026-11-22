using CustomPresenter.Domain.Bibles;

namespace CustomPresenter.Application.Bibles;

/// <summary>
/// Catálogo y sembrador de versiones bíblicas por defecto para el sistema.
/// </summary>
public static class DefaultBibleCatalog
{
    public static readonly IReadOnlyList<(Guid Id, string Name, string Abbreviation)> DefaultVersions =
    [
        (Guid.Parse("00000000-0000-0000-0000-000000000001"), "Reina Valera 1960", "RVR60"),
        (Guid.Parse("00000000-0000-0000-0000-000000000002"), "Reina Valera 1909", "RV1909"),
        (Guid.Parse("00000000-0000-0000-0000-000000000003"), "Nueva Versión Internacional", "NVI"),
        (Guid.Parse("00000000-0000-0000-0000-000000000004"), "Nueva Traducción Viviente", "NTV"),
        (Guid.Parse("00000000-0000-0000-0000-000000000005"), "Palabra de Dios para Todos", "PDT"),
    ];

    public static async Task EnsureDefaultsAsync(
        IBibleRepository bibleRepo,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bibleRepo);

        var existing = await bibleRepo.ListVersionsAsync(cancellationToken).ConfigureAwait(false);
        var existingAbbrs = existing
            .Select(x => x.Abbreviation.Trim().ToUpperInvariant())
            .ToHashSet();

        foreach (var (id, name, abbr) in DefaultVersions)
        {
            if (!existingAbbrs.Contains(abbr.ToUpperInvariant()))
            {
                var version = new BibleVersion(id, name, abbr, DateTimeOffset.UtcNow);
                await bibleRepo.SaveBibleAsync(version, [], cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
