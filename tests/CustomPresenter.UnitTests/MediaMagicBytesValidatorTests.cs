using CustomPresenter.Domain.Live;
using CustomPresenter.Domain.Media;
using CustomPresenter.UnitTests.Builders;
using Xunit;

namespace CustomPresenter.UnitTests;

public sealed class MediaMagicBytesValidatorTests
{
    [Fact]
    public void ValidateAceptaTodosLosFormatosPermitidosConMagicBytesReales()
    {
        // Arrange
        var cases = new (string FileName, byte[] Bytes, MediaKind Kind, MediaFormat Format)[]
        {
            ("fondo.png", MediaAssetBuilder.CreateValidPngBytes(), MediaKind.Image, MediaFormat.Png),
            ("fondo.jpg", MediaAssetBuilder.CreateValidJpegBytes(), MediaKind.Image, MediaFormat.Jpeg),
            ("fondo.webp", MediaAssetBuilder.CreateValidWebPBytes(), MediaKind.Image, MediaFormat.WebP),
            ("loop.mp4", MediaAssetBuilder.CreateValidMp4Bytes(), MediaKind.Video, MediaFormat.Mp4),
            ("loop.webm", MediaAssetBuilder.CreateValidWebMBytes(), MediaKind.Video, MediaFormat.WebM),
        };

        // Act & Assert
        foreach (var (fileName, bytes, expectedKind, expectedFormat) in cases)
        {
            var result = MediaMagicBytesValidator.Validate(fileName, bytes, bytes.LongLength);
            Assert.Equal(expectedKind, result.Kind);
            Assert.Equal(expectedFormat, result.Format);
        }
    }

    [Fact]
    public void ValidateRechazaExtensionFalsaConMagicBytesDeEjecutableOTexto()
    {
        // Arrange
        var fakePng = "MZ_NOT_A_PNG_EXECUTABLE"u8.ToArray();

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            MediaMagicBytesValidator.Validate("malicioso.png", fakePng, fakePng.LongLength));
    }

    [Fact]
    public void ValidateRechazaExtensionNoPermitidaYTamanoExcedido()
    {
        // Arrange
        var png = MediaAssetBuilder.CreateValidPngBytes();
        var mp4 = MediaAssetBuilder.CreateValidMp4Bytes();

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            MediaMagicBytesValidator.Validate("script.svg", png, png.LongLength));

        Assert.Throws<DomainException>(() =>
            MediaMagicBytesValidator.Validate("gigante.png", png, MediaConstants.MaxImageBytes + 1));

        Assert.Throws<DomainException>(() =>
            MediaMagicBytesValidator.Validate("gigante.mp4", mp4, MediaConstants.MaxVideoBytes + 1));
    }
}
