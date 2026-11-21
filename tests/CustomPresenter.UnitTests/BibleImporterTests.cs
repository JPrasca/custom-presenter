using CustomPresenter.Domain.Bibles;
using CustomPresenter.Domain.Live;
using CustomPresenter.Infrastructure.Importers;
using CustomPresenter.UnitTests.Builders;
using Xunit;

namespace CustomPresenter.UnitTests;

public sealed class BibleImporterTests
{
    private sealed class FakeOversizedStream(long totalBytesToEmit) : Stream
    {
        private long _emitted;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _emitted; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_emitted >= totalBytesToEmit)
            {
                return 0;
            }

            var toCopy = (int)Math.Min(count, totalBytesToEmit - _emitted);
            Array.Fill(buffer, (byte)'a', offset, toCopy);
            _emitted += toCopy;
            return toCopy;
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    [Fact]
    public async Task ImportAsyncConXmlOpenSongValidoParseaLibrosCapitulosYVersiculos()
    {
        // Arrange
        var importer = new BibleImporter();
        await using var stream = new BiblePayloadBuilder().BuildStream();

        // Act
        var result = await importer.ImportAsync(stream, "rvr60.xml", stream.Length);

        // Assert
        Assert.Equal("Reina Valera 1960", result.Version.Name);
        Assert.Equal("RVR60", result.Version.Abbreviation);
        Assert.Equal(2, result.Verses.Count);
        Assert.Equal("Génesis", result.Verses[0].BookName);
        Assert.Equal(1, result.Verses[0].BookNumber);
        Assert.Equal(3, result.Verses[1].Chapter);
        Assert.Equal(16, result.Verses[1].Verse);
    }

    [Fact]
    public async Task ImportAsyncConXmlZefaniaValidoParseaVersiculosCorrectamente()
    {
        // Arrange
        var importer = new BibleImporter();
        await using var stream = new BiblePayloadBuilder().AsZefaniaXml().BuildStream();

        // Act
        var result = await importer.ImportAsync(stream, "nvi.xml", stream.Length);

        // Assert
        Assert.Equal("Nueva Versión Internacional", result.Version.Name);
        Assert.Equal("NVI", result.Version.Abbreviation);
        Assert.Single(result.Verses);
        Assert.Equal(19, result.Verses[0].BookNumber);
        Assert.Equal("Salmos", result.Verses[0].BookName);
        Assert.Equal(23, result.Verses[0].Chapter);
        Assert.Equal(1, result.Verses[0].Verse);
    }

    [Fact]
    public async Task ImportAsyncConJsonValidoParseaVersionYVersiculos()
    {
        // Arrange
        var importer = new BibleImporter();
        await using var stream = new BiblePayloadBuilder().AsJson().BuildStream();

        // Act
        var result = await importer.ImportAsync(stream, "lbla.json", stream.Length);

        // Assert
        Assert.Equal("La Biblia de las Américas", result.Version.Name);
        Assert.Equal("LBLA", result.Version.Abbreviation);
        Assert.Single(result.Verses);
        Assert.Equal("Juan", result.Verses[0].BookName);
    }

    [Fact]
    public async Task ImportAsyncConPayloadXxeRechazaArchivoConDomainExceptionSinResolverEntidades()
    {
        // Arrange
        var importer = new BibleImporter();
        await using var stream = new BiblePayloadBuilder().AsXxeAttack().BuildStream();

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(
            () => importer.ImportAsync(stream, "xxe.xml", stream.Length));
    }

    [Fact]
    public async Task ImportAsyncConBillionLaughsRechazaArchivoSinExpandirMemoria()
    {
        // Arrange
        var importer = new BibleImporter();
        await using var stream = new BiblePayloadBuilder().AsBillionLaughsAttack().BuildStream();

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(
            () => importer.ImportAsync(stream, "dos.xml", stream.Length));
    }

    [Fact]
    public async Task ImportAsyncConStreamMayorA50MbOExtensionInvalidaLanzaDomainException()
    {
        // Arrange
        var importer = new BibleImporter();
        await using var oversized = new FakeOversizedStream(BibleImportLimits.MaxFileSizeBytes + 1024);
        await using var txtStream = new MemoryStream("hola"u8.ToArray());

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(
            () => importer.ImportAsync(oversized, "grande.xml", reportedSizeBytes: 10));
        await Assert.ThrowsAsync<DomainException>(
            () => importer.ImportAsync(txtStream, "biblia.txt", txtStream.Length));
    }
}
