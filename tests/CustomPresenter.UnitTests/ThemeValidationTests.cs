using CustomPresenter.Domain.Live;
using CustomPresenter.Domain.Themes;
using CustomPresenter.UnitTests.Builders;
using Xunit;

namespace CustomPresenter.UnitTests;

public sealed class ThemeValidationTests
{
    [Fact]
    public void CreateConColoresHexYFuentesValidasConstruyeTemasCorrectamente()
    {
        // Arrange & Act
        var settings = new ThemeSettingsBuilder()
            .WithProjector("Inter, sans-serif", 72, "#F1F5F9", "#090D16FF", TextAlignment.Left)
            .WithObs("Roboto", 36, "#FFFFFF", "#0F1115D1", LowerThirdPosition.Top, 80, 40, 12)
            .Build();

        // Assert
        Assert.Equal("Inter, sans-serif", settings.Projector.FontFamily);
        Assert.Equal(72, settings.Projector.FontSizePx);
        Assert.Equal("#F1F5F9", settings.Projector.TextColor);
        Assert.Equal(TextAlignment.Left, settings.Projector.Alignment);
        Assert.Equal(LowerThirdPosition.Top, settings.Obs.Position);
        Assert.Equal(36, settings.Obs.FontSizePx);
    }

    [Theory]
    [InlineData("#FFF")]
    [InlineData("red")]
    [InlineData("rgb(255,0,0)")]
    [InlineData("#12345G")]
    [InlineData("#123456; background: url(evil)")]
    public void CreateConColorNoHexadecimalLanzaDomainException(string invalidColor)
    {
        // Arrange & Act & Assert
        Assert.Throws<DomainException>(() =>
            ProjectorTheme.Create("Inter", 64, invalidColor, "#0B0D12", TextAlignment.Center));

        Assert.Throws<DomainException>(() =>
            ObsTheme.Create("Inter", 34, "#FFFFFF", invalidColor, LowerThirdPosition.Bottom, 64, 48, 10));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(161)]
    public void CreateConTamanoFuenteFueraDeRangoLanzaDomainException(int invalidSize)
    {
        // Arrange & Act & Assert
        Assert.Throws<DomainException>(() =>
            ProjectorTheme.Create("Inter", invalidSize, "#FFFFFF", "#0B0D12", TextAlignment.Center));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Inter; color: red")]
    [InlineData("Roboto { background: url(http://evil) }")]
    [InlineData("Arial</style><script>alert(1)</script>")]
    public void CreateConFuenteInseguraLanzaDomainException(string invalidFont)
    {
        // Arrange & Act & Assert
        Assert.Throws<DomainException>(() =>
            ProjectorTheme.Create(invalidFont, 64, "#FFFFFF", "#0B0D12", TextAlignment.Center));
    }
}
