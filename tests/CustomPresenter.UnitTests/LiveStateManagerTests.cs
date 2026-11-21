using CustomPresenter.Application.Live;
using CustomPresenter.Domain.Live;
using Xunit;

namespace CustomPresenter.UnitTests;

public sealed class LiveStateManagerTests
{
    [Fact]
    public async Task SmokeDomainYApplicationOperanCorrectamente()
    {
        var manager = new LiveStateManager([]);
        Assert.False(manager.Snapshot().IsTextVisible);

        var slide = new SlideContent("slide-1", "Prueba", "Alabado sea su nombre");
        var projected = await manager.ProjectSlideAsync(slide);
        Assert.True(projected.IsTextVisible);

        var cleared = await manager.ToggleClearTextAsync();
        Assert.False(cleared.IsTextVisible);
        Assert.NotNull(cleared.CurrentSlide);
    }

    [Fact]
    public async Task ProjectSlideAsyncConTextoVacioLanzaDomainException()
    {
        var manager = new LiveStateManager([]);
        var emptySlide = new SlideContent("slide-empty", "Vacía", "   ");

        await Assert.ThrowsAsync<DomainException>(() => manager.ProjectSlideAsync(emptySlide));
    }
}
