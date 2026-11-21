using CustomPresenter.Application.Live;
using CustomPresenter.Domain.Live;
using Xunit;

namespace CustomPresenter.UnitTests;

public sealed class LiveStateManagerTests
{
    [Fact]
    public async Task ProjectSlideAsyncConContenidoValidoActualizaEstadoYLimpiaBanderas()
    {
        var manager = new LiveStateManager([]);
        await manager.ToggleBlackoutAsync();
        await manager.ToggleLogoAsync();

        var slide = new SlideContent("slide-1", "Prueba", "Alabado sea su nombre");
        var projected = await manager.ProjectSlideAsync(slide);

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
        var manager = new LiveStateManager([]);
        var slide = new SlideContent("slide-1", "Prueba", "Texto de prueba");
        await manager.ProjectSlideAsync(slide);

        var cleared = await manager.ToggleClearTextAsync();
        Assert.False(cleared.IsTextVisible);
        Assert.Equal(EffectiveDisplayMode.ClearText, cleared.EffectiveMode);
        Assert.Equal(slide, cleared.CurrentSlide);

        var restored = await manager.ToggleClearTextAsync();
        Assert.True(restored.IsTextVisible);
        Assert.Equal(EffectiveDisplayMode.Slide, restored.EffectiveMode);
    }

    [Fact]
    public async Task ToggleLogoAsyncYToggleBlackoutAsyncRespetanJerarquia()
    {
        var manager = new LiveStateManager([]);
        var slide = new SlideContent("slide-1", "Prueba", "Texto de prueba");
        await manager.ProjectSlideAsync(slide);

        var withLogo = await manager.ToggleLogoAsync();
        Assert.Equal(EffectiveDisplayMode.Logo, withLogo.EffectiveMode);
        Assert.False(withLogo.IsTextVisible);

        var withBlackout = await manager.ToggleBlackoutAsync();
        Assert.Equal(EffectiveDisplayMode.Blackout, withBlackout.EffectiveMode);
        Assert.False(withBlackout.IsTextVisible);

        var blackoutOff = await manager.ToggleBlackoutAsync();
        Assert.Equal(EffectiveDisplayMode.Logo, blackoutOff.EffectiveMode);
    }

    [Fact]
    public async Task ProjectSlideAsyncConIdVacioOTextoVacioLanzaDomainException()
    {
        var manager = new LiveStateManager([]);

        await Assert.ThrowsAsync<DomainException>(
            () => manager.ProjectSlideAsync(new SlideContent("", "Título", "Texto válido")));

        await Assert.ThrowsAsync<DomainException>(
            () => manager.ProjectSlideAsync(new SlideContent("id-1", "Título", "   ")));
    }

    [Fact]
    public async Task ProjectSlideAsyncConTextoExcediendoLimiteLanzaDomainException()
    {
        var manager = new LiveStateManager([]);
        var oversizedText = new string('A', SlideContent.MaxTextLength + 1);

        await Assert.ThrowsAsync<DomainException>(
            () => manager.ProjectSlideAsync(new SlideContent("id-1", "Título", oversizedText)));
    }
}
