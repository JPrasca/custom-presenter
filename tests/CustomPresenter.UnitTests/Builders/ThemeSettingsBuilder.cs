using CustomPresenter.Domain.Themes;

namespace CustomPresenter.UnitTests.Builders;

internal sealed class ThemeSettingsBuilder
{
    private ProjectorTheme _projector = ProjectorTheme.Default;
    private ObsTheme _obs = ObsTheme.Default;

    public ThemeSettingsBuilder WithProjector(
        string fontFamily = "Inter",
        int fontSizePx = 64,
        string textColor = "#FFFFFF",
        string backgroundColor = "#0B0D12",
        TextAlignment alignment = TextAlignment.Center)
    {
        _projector = ProjectorTheme.Create(fontFamily, fontSizePx, textColor, backgroundColor, alignment);
        return this;
    }

    public ThemeSettingsBuilder WithObs(
        string fontFamily = "Roboto",
        int fontSizePx = 34,
        string textColor = "#F8FAFC",
        string boxBackgroundColor = "#0F1115CC",
        LowerThirdPosition position = LowerThirdPosition.Bottom,
        int marginHorizontalPx = 64,
        int marginVerticalPx = 48,
        int borderRadiusPx = 10)
    {
        _obs = ObsTheme.Create(
            fontFamily,
            fontSizePx,
            textColor,
            boxBackgroundColor,
            position,
            marginHorizontalPx,
            marginVerticalPx,
            borderRadiusPx);
        return this;
    }

    public ThemeSettings Build() => new(_projector, _obs);
}
