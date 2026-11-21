using CustomPresenter.Domain.Live;
using CustomPresenter.Domain.Songs;
using Xunit;

namespace CustomPresenter.UnitTests;

public sealed class SongLyricsSplitterTests
{
    [Fact]
    public void SplitDividePorLineasEnBlancoSimplesYMultiplesPreservandoSaltosInternos()
    {
        // Arrange
        const string rawLyrics =
            "  Línea 1 estrofa 1  \r\n" +
            "Línea 2 estrofa 1\r\n" +
            "\r\n" +
            "   \r\n" +
            "Línea 1 estrofa 2\n" +
            "Línea 2 estrofa 2\n\n\n" +
            "Estrofa 3";

        // Act
        var slides = SongLyricsSplitter.Split(rawLyrics);

        // Assert
        Assert.Equal(3, slides.Count);
        Assert.Equal(0, slides[0].Index);
        Assert.Equal("Línea 1 estrofa 1\nLínea 2 estrofa 1", slides[0].Text);
        Assert.Equal(1, slides[1].Index);
        Assert.Equal("Línea 1 estrofa 2\nLínea 2 estrofa 2", slides[1].Text);
        Assert.Equal(2, slides[2].Index);
        Assert.Equal("Estrofa 3", slides[2].Text);
    }

    [Fact]
    public void SplitConLetraVaciaOSoloEspaciosLanzaDomainException()
    {
        // Arrange
        const string blankLyrics = "   \r\n\t\n   ";

        // Act & Assert
        Assert.Throws<DomainException>(() => SongLyricsSplitter.Split(blankLyrics));
    }

    [Fact]
    public void SongCreateConTituloVacioOExcediendoLimiteLanzaDomainException()
    {
        // Arrange
        var longTitle = new string('T', Song.MaxTitleLength + 1);

        // Act & Assert
        Assert.Throws<DomainException>(() => Song.Create("   ", null, null, "Estrofa válida"));
        Assert.Throws<DomainException>(() => Song.Create(longTitle, null, null, "Estrofa válida"));
    }

    [Fact]
    public void SplitConEstrofaExcediendoMaxSlideTextLengthLanzaDomainException()
    {
        // Arrange
        var oversizedVerse = new string('X', SlideContent.MaxTextLength + 1);

        // Act & Assert
        Assert.Throws<DomainException>(() => SongLyricsSplitter.Split(oversizedVerse));
    }
}
