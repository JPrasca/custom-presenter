using CustomPresenter.Application.Live;
using CustomPresenter.Domain.Live;
using CustomPresenter.Domain.Ports;
using Xunit;

namespace CustomPresenter.IntegrationTests;

public sealed class LiveBroadcasterIntegrationTests
{
    private sealed class SpyBroadcaster : ILiveBroadcaster
    {
        public List<LiveState> Received { get; } = [];

        public ValueTask BroadcastAsync(LiveState state, CancellationToken cancellationToken = default)
        {
            Received.Add(state);
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task LiveStateManagerNotificaASuscriptoresEnCadaTransicion()
    {
        var spy = new SpyBroadcaster();
        var manager = new LiveStateManager([spy]);

        var slide = new SlideContent("int-1", "Salmo 23", "El Señor es mi pastor", "Salmos 23:1");
        await manager.ProjectSlideAsync(slide);
        await manager.ToggleBlackoutAsync();

        Assert.Equal(2, spy.Received.Count);
        Assert.True(spy.Received[0].IsTextVisible);
        Assert.True(spy.Received[1].Flags.Blackout);
        Assert.False(spy.Received[1].IsTextVisible);
    }
}
