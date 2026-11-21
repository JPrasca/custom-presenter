using CustomPresenter.Domain.Bibles;
using Xunit;

namespace CustomPresenter.UnitTests;

public sealed class BibleReferenceParserTests
{
    [Theory]
    [InlineData("Juan 3:16", 43, 3, 16, null)]
    [InlineData("Jn 3:16", 43, 3, 16, null)]
    [InlineData("Salmos 23:1-3", 19, 23, 1, 3)]
    [InlineData("Sal 23:1", 19, 23, 1, null)]
    [InlineData("Genesis 1:1", 1, 1, 1, null)]
    [InlineData("Génesis 1:1", 1, 1, 1, null)]
    [InlineData("1 Corintios 13:4-7", 46, 13, 4, 7)]
    [InlineData("I Corintios 13:4", 46, 13, 4, null)]
    [InlineData("II Timoteo 1:7", 55, 1, 7, null)]
    [InlineData("III Juan 1:2", 64, 1, 2, null)]
    [InlineData("Salmos 23", 19, 23, null, null)]
    public void TryParseReconoceCitasAbreviaturasNumerosRomanosYRangos(
        string input,
        int expectedBookNumber,
        int expectedChapter,
        int? expectedStartVerse,
        int? expectedEndVerse)
    {
        // Arrange & Act
        var parsed = BibleReferenceParser.TryParse(input, out var reference);

        // Assert
        Assert.True(parsed);
        Assert.NotNull(reference);
        Assert.Equal(expectedBookNumber, reference.CanonicalBookNumber);
        Assert.Equal(expectedChapter, reference.Chapter);
        Assert.Equal(expectedStartVerse, reference.StartVerse);
        Assert.Equal(expectedEndVerse, reference.EndVerse);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("porque de tal manera amo Dios")]
    [InlineData("pastor delicados")]
    [InlineData("Juan 3:16-10")]
    public void TryParseConTextoLibreORangoInvertidoDevuelveFalse(string input)
    {
        // Arrange & Act
        var parsed = BibleReferenceParser.TryParse(input, out var reference);

        // Assert
        Assert.False(parsed);
        Assert.Null(reference);
    }
}
