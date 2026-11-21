using System.Text.Json;
using CustomPresenter.Domain.Backup;
using CustomPresenter.Domain.Live;
using CustomPresenter.Infrastructure.Importers;

namespace CustomPresenter.Infrastructure.Backup;

/// <summary>
/// Serializador JSON acotado a 50 MB con validación UTF-8 estricta y esquema versionado.
/// </summary>
public sealed class JsonBackupSerializer : IBackupSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        MaxDepth = 16,
    };

    public async Task SerializeAsync(
        BackupPayload payload,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(destination);

        await JsonSerializer.SerializeAsync(destination, payload, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<BackupPayload> DeserializeAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var reportedLength = source.CanSeek ? source.Length : 0L;
        var json = await StreamReadHelper
            .ReadUtf8BoundedAsync(source, reportedLength, BackupPayload.MaxBackupBytes, cancellationToken)
            .ConfigureAwait(false);

        BackupPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<BackupPayload>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new DomainException("El archivo de respaldo JSON tiene un formato inválido.", ex);
        }

        if (payload is null)
        {
            throw new DomainException("El archivo de respaldo JSON está vacío.");
        }

        if (payload.SchemaVersion != BackupPayload.CurrentSchemaVersion)
        {
            throw new DomainException(
                $"Versión de esquema de respaldo no soportada: {payload.SchemaVersion}.");
        }

        if (payload.Songs is null || payload.Announcements is null)
        {
            throw new DomainException(
                "El archivo de respaldo debe incluir las colecciones 'songs' y 'announcements'.");
        }

        return payload;
    }
}
