using CustomPresenter.Application.Live;
using CustomPresenter.Application.Themes;
using CustomPresenter.Domain.Themes;
using CustomPresenter.UnitTests.Builders;
using Xunit;

namespace CustomPresenter.UnitTests;

public sealed class ThemeServiceTests
{
    private sealed class InMemoryThemeRepository : IThemeRepository
    {
        public ThemeSettings Stored { get; private set; } = ThemeSettings.Default;

        public Task<ThemeSettings> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Stored);

        public Task SaveAsync(ThemeSettings settings, CancellationToken cancellationToken = default)
        {
            Stored = settings;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task UpdateThemesPropagaEnTiempoRealEnLiveStateManagerConservandoDiapositiva()
    {
        // Arrange
        var repo = new InMemoryThemeRepository();
        var service = new ThemeService(repo);
        var broadcaster = new ChannelLiveBroadcaster();
        var liveManager = new LiveStateManager([broadcaster]);
        using var sub = broadcaster.Subscribe();

        var slide = new SlideContentBuilder().WithText("Salmo 23").Build();
        await liveManager.ProjectSlideAsync(slide);

        var newProjector = ProjectorTheme.Create("Montserrat", 80, "#FFE082", "#10141F", TextAlignment.Right);
        var newObs = ObsTheme.Create("Inter", 38, "#FFFFFF", "#000000B3", LowerThirdPosition.Top, 48, 32, 16);

        // Act
        var afterProjector = await service.UpdateProjectorThemeAsync(newProjector, liveManager);
        var afterObs = await service.UpdateObsThemeAsync(newObs, liveManager);

        // Assert
        Assert.Equal("Salmo 23", afterObs.CurrentSlide?.Text);
        Assert.Equal(80, afterProjector.EffectiveTheme.Projector.FontSizePx);
        Assert.Equal("#FFE082", afterObs.EffectiveTheme.Projector.TextColor);
        Assert.Equal(LowerThirdPosition.Top, afterObs.EffectiveTheme.Obs.Position);
        Assert.Equal(38, repo.Stored.Obs.FontSizePx);
    }

    [Fact]
    public async Task ProjectSlideAsyncConservaTemaPersonalizadoActivo()
    {
        // Arrange
        var repo = new InMemoryThemeRepository();
        var custom = new ThemeSettingsBuilder()
            .WithProjector("Inter", 90, "#00FFAA", "#000000", TextAlignment.Center)
            .Build();
        await repo.SaveAsync(custom);

        var service = new ThemeService(repo);
        var liveManager = new LiveStateManager([new ChannelLiveBroadcaster()]);
        await service.LoadAndApplyAsync(liveManager);

        // Act
        var state = await liveManager.ProjectSlideAsync(new SlideContentBuilder().WithText("Aleluya").Build());

        // Assert
        Assert.Equal(90, state.EffectiveTheme.Projector.FontSizePx);
        Assert.Equal("#00FFAA", state.EffectiveTheme.Projector.TextColor);
    }
}
