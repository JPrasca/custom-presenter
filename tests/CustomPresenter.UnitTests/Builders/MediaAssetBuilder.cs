using CustomPresenter.Domain.Media;

namespace CustomPresenter.UnitTests.Builders;

internal sealed class MediaAssetBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _originalName = "fondo-cielo.png";
    private MediaKind _kind = MediaKind.Image;
    private MediaFormat _format = MediaFormat.Png;
    private string _contentType = "image/png";
    private long _sizeBytes = 1024;

    public MediaAssetBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public MediaAssetBuilder WithOriginalName(string originalName)
    {
        _originalName = originalName;
        return this;
    }

    public MediaAssetBuilder AsVideoMp4()
    {
        _kind = MediaKind.Video;
        _format = MediaFormat.Mp4;
        _contentType = "video/mp4";
        _originalName = "loop-adoracion.mp4";
        return this;
    }

    public MediaAsset Build()
    {
        var ext = MediaMagicBytesValidator.GetExtension(_format);
        return new MediaAsset(
            _id,
            _originalName,
            _kind,
            _format,
            $"{_id:D}.{ext}",
            _contentType,
            _sizeBytes,
            DateTimeOffset.UtcNow);
    }

    public static byte[] CreateValidPngBytes() =>
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];

    public static byte[] CreateValidJpegBytes() =>
        [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];

    public static byte[] CreateValidWebPBytes() =>
        [0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50];

    public static byte[] CreateValidMp4Bytes() =>
        [0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70, 0x6D, 0x70, 0x34, 0x32];

    public static byte[] CreateValidWebMBytes() =>
        [0x1A, 0x45, 0xDF, 0xA3, 0x9F, 0x42, 0x86, 0x81, 0x01, 0x42, 0xF7, 0x81];
}
