namespace CustomPresenter.Domain.Backup;

/// <summary>
/// Puerto de serialización y deserialización validada de respaldos JSON.
/// </summary>
public interface IBackupSerializer
{
    Task SerializeAsync(
        BackupPayload payload,
        Stream destination,
        CancellationToken cancellationToken = default);

    Task<BackupPayload> DeserializeAsync(
        Stream source,
        CancellationToken cancellationToken = default);
}
