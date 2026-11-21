using CustomPresenter.Application.Live;
using CustomPresenter.Domain.Live;
using Xunit;

namespace CustomPresenter.IntegrationTests;

public sealed class LiveBroadcasterIntegrationTests
{
    [Fact]
    public async Task ChannelLiveBroadcasterNotificaAMultiplesSuscriptoresEnOrden()
    {
        var broadcaster = new ChannelLiveBroadcaster();
        var manager = new LiveStateManager([broadcaster]);

        using var subProjector = broadcaster.Subscribe();
        using var subObs = broadcaster.Subscribe();

        var slide = new SlideContent("int-1", "Salmo 23", "El Señor es mi pastor", "Salmos 23:1");
        await manager.ProjectSlideAsync(slide);
        await manager.ToggleClearTextAsync();
        await manager.ToggleLogoAsync();
        await manager.ToggleBlackoutAsync();

        var projectorEvents = new List<LiveState>();
        var obsEvents = new List<LiveState>();

        for (var i = 0; i < 4; i++)
        {
            projectorEvents.Add(await subProjector.Reader.ReadAsync());
            obsEvents.Add(await subObs.Reader.ReadAsync());
        }

        Assert.Equal(4, projectorEvents.Count);
        Assert.Equal(4, obsEvents.Count);
        Assert.Equal(EffectiveDisplayMode.Slide, projectorEvents[0].EffectiveMode);
        Assert.Equal(EffectiveDisplayMode.ClearText, projectorEvents[1].EffectiveMode);
        Assert.Equal(EffectiveDisplayMode.Logo, projectorEvents[2].EffectiveMode);
        Assert.Equal(EffectiveDisplayMode.Blackout, projectorEvents[3].EffectiveMode);
        Assert.Equal(projectorEvents, obsEvents);
    }

    [Fact]
    public async Task ChannelLiveBroadcasterAislaSuscriptorDesconectadoDeLosDemas()
    {
        var broadcaster = new ChannelLiveBroadcaster();
        var manager = new LiveStateManager([broadcaster]);

        var subDisposed = broadcaster.Subscribe();
        using var subActive = broadcaster.Subscribe();

        subDisposed.Dispose();

        var slide = new SlideContent("int-2", "Anuncio", "Bienvenidos");
        await manager.ProjectSlideAsync(slide);

        var received = await subActive.Reader.ReadAsync();
        Assert.Equal(slide, received.CurrentSlide);
        Assert.Equal(EffectiveDisplayMode.Slide, received.EffectiveMode);
    }
}
