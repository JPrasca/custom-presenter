using CustomPresenter.Domain.Live;

namespace CustomPresenter.Domain.Ports;

/// <summary>
/// Puerto de salida para difundir cambios de estado en vivo hacia las vistas (Proyector y OBS).
/// </summary>
public interface ILiveBroadcaster
{
    ValueTask BroadcastAsync(LiveState state, CancellationToken cancellationToken = default);
}
