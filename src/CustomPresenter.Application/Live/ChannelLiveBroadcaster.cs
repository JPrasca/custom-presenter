using System.Collections.Concurrent;
using System.Threading.Channels;
using CustomPresenter.Domain.Live;
using CustomPresenter.Domain.Ports;

namespace CustomPresenter.Application.Live;

/// <summary>
/// Suscripción individual de lectura para recibir actualizaciones de estado en vivo.
/// </summary>
public sealed class LiveSubscription : IDisposable
{
    private readonly Action _onDispose;
    private int _disposed;

    internal LiveSubscription(ChannelReader<LiveState> reader, Action onDispose)
    {
        Reader = reader;
        _onDispose = onDispose;
    }

    public ChannelReader<LiveState> Reader { get; }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _onDispose();
        }
    }
}

/// <summary>
/// Difusor no bloqueante basado en canales acotados (DropOldest, capacidad 64).
/// </summary>
public sealed class ChannelLiveBroadcaster : ILiveBroadcaster
{
    public const int SubscriberChannelCapacity = 64;

    private readonly ConcurrentDictionary<Guid, Channel<LiveState>> _subscribers = new();

    /// <summary>
    /// Registra un nuevo consumidor para recibir transiciones de estado sin bloquear al emisor.
    /// </summary>
    public LiveSubscription Subscribe()
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<LiveState>(new BoundedChannelOptions(SubscriberChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

        _subscribers.TryAdd(id, channel);

        return new LiveSubscription(channel.Reader, () =>
        {
            if (_subscribers.TryRemove(id, out var removed))
            {
                removed.Writer.TryComplete();
            }
        });
    }

    public ValueTask BroadcastAsync(LiveState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled(cancellationToken);
        }

        foreach (var entry in _subscribers)
        {
            entry.Value.Writer.TryWrite(state);
        }

        return ValueTask.CompletedTask;
    }
}
