using CustomPresenter.Domain.Songs;
using Xunit;

namespace CustomPresenter.UnitTests;

public sealed class SongLyricsFormatterTests
{
    [Fact]
    public void FormatEliminaMarcasDeTiempoNumericasYPreservaEncabezados()
    {
        // Arrange
        const string raw = """
            [Verso]
            [00:12.34] Sublime gracia del Señor
            [00:16.500] Que a un infeliz salvó
            [Coro]
            [00:22.10] Fui ciego mas hoy veo yo
            """;

        // Act
        var formatted = SongLyricsFormatter.Format(raw);

        // Assert
        Assert.DoesNotContain("[00:12.34]", formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("[00:16.500]", formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("[00:22.10]", formatted, StringComparison.Ordinal);
        Assert.Contains("[Verso]", formatted, StringComparison.Ordinal);
        Assert.Contains("[Coro]", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatDivideBloquesLargosEnEstrofasDeMaximoCuatroLineas()
    {
        // Arrange
        const string raw = "Línea 1\nLínea 2\nLínea 3\nLínea 4\nLínea 5\nLínea 6";

        // Act
        var formatted = SongLyricsFormatter.Format(raw);
        var slides = SongLyricsSplitter.Split(formatted);

        // Assert
        Assert.Equal(2, slides.Count);
        Assert.Equal("Línea 1\nLínea 2\nLínea 3\nLínea 4", slides[0].Text);
        Assert.Equal("Línea 5\nLínea 6", slides[1].Text);
    }
}
