using CustomPresenter.Application.Live;
using CustomPresenter.Domain.Live;
using CustomPresenter.UnitTests.Builders;
using Xunit;

namespace CustomPresenter.UnitTests;

public sealed class LiveStateManagerTests
{
    [Fact]
    public async Task ProjectSlideAsyncConContenidoValidoActualizaEstadoYLimpiaBanderas()
    {
        // Arrange
        var manager = new LiveStateManager([]);
        await manager.ToggleBlackoutAsync();
        await manager.ToggleLogoAsync();
        var slide = new SlideContentBuilder().Build();

        // Act
        var projected = await manager.ProjectSlideAsync(slide);

        // Assert
        Assert.Equal(slide, projected.CurrentSlide);
        Assert.True(projected.IsTextVisible);
        Assert.Equal(EffectiveDisplayMode.Slide, projected.EffectiveMode);
        Assert.False(projected.Flags.Blackout);
        Assert.False(projected.Flags.Logo);
        Assert.False(projected.Flags.ClearText);
    }

    [Fact]
    public async Task ToggleClearTextAsyncOcultaTextoManteniendoDiapositivaActiva()
    {
        // Arrange
        var manager = new LiveStateManager([]);
        var slide = new SlideContentBuilder().WithText("Texto de prueba").Build();
        await manager.ProjectSlideAsync(slide);

        // Act
        var cleared = await manager.ToggleClearTextAsync();
        var restored = await manager.ToggleClearTextAsync();

        // Assert
        Assert.False(cleared.IsTextVisible);
        Assert.Equal(EffectiveDisplayMode.ClearText, cleared.EffectiveMode);
        Assert.Equal(slide, cleared.CurrentSlide);

        Assert.True(restored.IsTextVisible);
        Assert.Equal(EffectiveDisplayMode.Slide, restored.EffectiveMode);
    }

    [Fact]
    public async Task ToggleLogoAsyncYToggleBlackoutAsyncRespetanJerarquia()
    {
        // Arrange
        var manager = new LiveStateManager([]);
        var slide = new SlideContentBuilder().WithText("Texto de prueba").Build();
        await manager.ProjectSlideAsync(slide);

        // Act
        var withLogo = await manager.ToggleLogoAsync();
        var withBlackout = await manager.ToggleBlackoutAsync();
        var blackoutOff = await manager.ToggleBlackoutAsync();

        // Assert
        Assert.Equal(EffectiveDisplayMode.Logo, withLogo.EffectiveMode);
        Assert.False(withLogo.IsTextVisible);
        Assert.Equal(EffectiveDisplayMode.Blackout, withBlackout.EffectiveMode);
        Assert.False(withBlackout.IsTextVisible);
        Assert.Equal(EffectiveDisplayMode.Logo, blackoutOff.EffectiveMode);
    }

    [Fact]
    public async Task ProjectSlideAsyncConIdVacioOTextoVacioLanzaDomainException()
    {
        // Arrange
        var manager = new LiveStateManager([]);
        var slideIdVacio = new SlideContentBuilder().WithId("").Build();
        var slideTextoVacio = new SlideContentBuilder().WithText("   ").Build();

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(() => manager.ProjectSlideAsync(slideIdVacio));
        await Assert.ThrowsAsync<DomainException>(() => manager.ProjectSlideAsync(slideTextoVacio));
    }

    [Fact]
    public async Task ProjectSlideAsyncConTextoExcediendoLimiteLanzaDomainException()
    {
        // Arrange
        var manager = new LiveStateManager([]);
        var oversizedText = new string('A', SlideContent.MaxTextLength + 1);
        var slide = new SlideContentBuilder().WithText(oversizedText).Build();

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(() => manager.ProjectSlideAsync(slide));
    }
}
