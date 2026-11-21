using System.Text;
using CustomPresenter.Domain.Live;
using CustomPresenter.Domain.Songs;
using CustomPresenter.Infrastructure.Importers;
using Xunit;

namespace CustomPresenter.UnitTests;

public sealed class SongImporterTests
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
    public async Task ImportAsyncConTxtValidoParseaMultiplesCanciones()
    {
        // Arrange
        var importer = new SongImporter();
        const string content = """
            Cuán Grande Es Él
            Señor mi Dios al contemplar los cielos

            Mi corazón entona la canción
            ---
            Sublime Gracia
            Sublime gracia del Señor
            """;
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        // Act
        var songs = await importer.ImportAsync(stream, "canciones.txt", stream.Length);

        // Assert
        Assert.Equal(2, songs.Count);
        Assert.Equal("Cuán Grande Es Él", songs[0].Title);
        Assert.Equal(2, songs[0].Slides.Count);
        Assert.Equal("Sublime Gracia", songs[1].Title);
    }

    [Fact]
    public async Task ImportAsyncConJsonValidoParseaListaDeCanciones()
    {
        // Arrange
        var importer = new SongImporter();
        const string json = """
            [
              {
                "title": "Alabanza",
                "author": "Anónimo",
                "musicalKey": "D",
                "rawLyrics": "Te alabamos hoy\n\nCon todo el corazón"
              }
            ]
            """;
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        // Act
        var songs = await importer.ImportAsync(stream, "canciones.json", stream.Length);

        // Assert
        Assert.Single(songs);
        Assert.Equal("Alabanza", songs[0].Title);
        Assert.Equal("Anónimo", songs[0].Author);
        Assert.Equal(2, songs[0].Slides.Count);
    }

    [Fact]
    public async Task ImportAsyncConStreamQueSupera50MbRealesAunqueReportaMenosLanzaDomainException()
    {
        // Arrange
        var importer = new SongImporter();
        await using var oversizedStream = new FakeOversizedStream(SongImportLimits.MaxFileSizeBytes + 1024);

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(
            () => importer.ImportAsync(oversizedStream, "grande.txt", reportedSizeBytes: 100));
    }

    [Fact]
    public async Task ImportAsyncConJsonAnidamientoProfundoMayorA16LanzaDomainException()
    {
        // Arrange
        var importer = new SongImporter();
        var deepJson = new string('[', 20) + "{}" + new string(']', 20);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(deepJson));

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(
            () => importer.ImportAsync(stream, "profundo.json", stream.Length));
    }

    [Fact]
    public async Task ImportAsyncConExtensionNoSoportadaOUtf8InvalidoLanzaDomainException()
    {
        // Arrange
        var importer = new SongImporter();
        await using var xmlStream = new MemoryStream(Encoding.UTF8.GetBytes("<song />"));
        await using var invalidUtf8 = new MemoryStream([0xFF, 0xFE, 0xFD]);

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(
            () => importer.ImportAsync(xmlStream, "canciones.xml", xmlStream.Length));
        await Assert.ThrowsAsync<DomainException>(
            () => importer.ImportAsync(invalidUtf8, "corrupto.txt", invalidUtf8.Length));
    }
}
