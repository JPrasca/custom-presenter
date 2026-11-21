using CustomPresenter.Domain.Songs;
using Xunit;

namespace CustomPresenter.UnitTests;

public sealed class Fts5QuerySanitizerTests
{
    [Fact]
    public void SanitizeEnvuelveTokensAlfanumericosComoPrefijosSeguros()
    {
        // Arrange
        const string rawQuery = "  Sublime Gracia ";

        // Act
        var sanitized = Fts5QuerySanitizer.Sanitize(rawQuery);

        // Assert
        Assert.Equal("\"Sublime\"* \"Gracia\"*", sanitized);
    }

    [Fact]
    public void SanitizeNeutralizaOperadoresYPuntuacionPeligrosaDeFts5()
    {
        // Arrange
        const string maliciousQuery = "title:\"Dios\" OR NOT NEAR(amor*, 5) : ^";

        // Act
        var sanitized = Fts5QuerySanitizer.Sanitize(maliciousQuery);

        // Assert
        Assert.DoesNotContain(":", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("^", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("(", sanitized, StringComparison.Ordinal);
        Assert.Contains("\"Dios\"*", sanitized, StringComparison.Ordinal);
        Assert.Contains("\"amor\"*", sanitized, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("*** ::: \"\" ()")]
    public void SanitizeConEntradaVaciaOSoloSimbolosDevuelveCadenaVacia(string input)
    {
        // Arrange & Act
        var sanitized = Fts5QuerySanitizer.Sanitize(input);

        // Assert
        Assert.Equal(string.Empty, sanitized);
    }
}
