using CustomPresenter.Domain.Live;
using CustomPresenter.UnitTests.Builders;
using Xunit;

namespace CustomPresenter.UnitTests;

public sealed class LiveStateTests
{
    [Fact]
    public void EmptyStateTieneModoIdleYTextoOculto()
    {
        // Arrange
        var state = LiveState.Empty;

        // Act
        var mode = state.EffectiveMode;
        var isVisible = state.IsTextVisible;

        // Assert
        Assert.Null(state.CurrentSlide);
        Assert.False(isVisible);
        Assert.Equal(EffectiveDisplayMode.Idle, mode);
    }

    [Fact]
    public void PrioridadDeBanderasRespetaBlackoutSobreLogoYClearText()
    {
        // Arrange
        var slide = new SlideContentBuilder().WithId("s-1").WithText("Letra").Build();
        var soloSlide = new LiveState(slide, new LiveFlags());
        var conClear = new LiveState(slide, new LiveFlags(ClearText: true));
        var conLogoYClear = new LiveState(slide, new LiveFlags(ClearText: true, Logo: true));
        var conBlackoutYTodo = new LiveState(
            slide,
            new LiveFlags(ClearText: true, Blackout: true, Logo: true));

        // Act & Assert
        Assert.Equal(EffectiveDisplayMode.Slide, soloSlide.EffectiveMode);
        Assert.True(soloSlide.IsTextVisible);

        Assert.Equal(EffectiveDisplayMode.ClearText, conClear.EffectiveMode);
        Assert.False(conClear.IsTextVisible);

        Assert.Equal(EffectiveDisplayMode.Logo, conLogoYClear.EffectiveMode);
        Assert.False(conLogoYClear.IsTextVisible);

        Assert.Equal(EffectiveDisplayMode.Blackout, conBlackoutYTodo.EffectiveMode);
        Assert.False(conBlackoutYTodo.IsTextVisible);
    }

    [Fact]
    public void BlackoutTienePrioridadInclusoSinDiapositivaActiva()
    {
        // Arrange
        var state = new LiveState(null, new LiveFlags(Blackout: true, Logo: true));

        // Act
        var mode = state.EffectiveMode;
        var isVisible = state.IsTextVisible;

        // Assert
        Assert.Equal(EffectiveDisplayMode.Blackout, mode);
        Assert.False(isVisible);
    }
}
