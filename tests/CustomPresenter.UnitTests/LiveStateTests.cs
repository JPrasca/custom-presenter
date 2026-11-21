using CustomPresenter.Domain.Live;
using Xunit;

namespace CustomPresenter.UnitTests;

public sealed class LiveStateTests
{
    [Fact]
    public void EmptyStateTieneModoIdleYTextoOculto()
    {
        var state = LiveState.Empty;

        Assert.Null(state.CurrentSlide);
        Assert.False(state.IsTextVisible);
        Assert.Equal(EffectiveDisplayMode.Idle, state.EffectiveMode);
    }

    [Fact]
    public void PrioridadDeBanderasRespetaBlackoutSobreLogoYClearText()
    {
        var slide = new SlideContent("s-1", "Título", "Letra");

        var soloSlide = new LiveState(slide, new LiveFlags());
        Assert.Equal(EffectiveDisplayMode.Slide, soloSlide.EffectiveMode);
        Assert.True(soloSlide.IsTextVisible);

        var conClear = new LiveState(slide, new LiveFlags(ClearText: true));
        Assert.Equal(EffectiveDisplayMode.ClearText, conClear.EffectiveMode);
        Assert.False(conClear.IsTextVisible);

        var conLogoYClear = new LiveState(slide, new LiveFlags(ClearText: true, Logo: true));
        Assert.Equal(EffectiveDisplayMode.Logo, conLogoYClear.EffectiveMode);
        Assert.False(conLogoYClear.IsTextVisible);

        var conBlackoutYTodo = new LiveState(
            slide,
            new LiveFlags(ClearText: true, Blackout: true, Logo: true));
        Assert.Equal(EffectiveDisplayMode.Blackout, conBlackoutYTodo.EffectiveMode);
        Assert.False(conBlackoutYTodo.IsTextVisible);
    }

    [Fact]
    public void BlackoutTienePrioridadInclusoSinDiapositivaActiva()
    {
        var state = new LiveState(null, new LiveFlags(Blackout: true, Logo: true));

        Assert.Equal(EffectiveDisplayMode.Blackout, state.EffectiveMode);
        Assert.False(state.IsTextVisible);
    }
}
