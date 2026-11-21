using CustomPresenter.Application.Live;
using CustomPresenter.Domain.Live;
using CustomPresenter.Domain.Media;

namespace CustomPresenter.Application.Media;

/// <summary>
/// Casos de uso para importación segura, listado, asignación y borrado de fondos multimedia.
/// </summary>
public sealed class MediaLibraryService
{
    private readonly IMediaRepository _repository;
    private readonly IMediaStorage _storage;

    public MediaLibraryService(IMediaRepository repository, IMediaStorage storage)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(storage);
        _repository = repository;
        _storage = storage;
    }

    public async Task<MediaAsset> ImportAsync(
        Stream content,
        string originalFileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var header = new byte[MediaConstants.HeaderBytesRequired];
        var headerRead = await ReadHeaderAsync(content, header, cancellationToken).ConfigureAwait(false);
        if (headerRead <= 0)
        {
            throw new DomainException("El archivo multimedia está vacío.");
        }

        // Validamos extensión y magic bytes antes de escribir en disco.
        var validation = MediaMagicBytesValidator.Validate(
            originalFileName,
            header.AsSpan(0, headerRead),
            headerRead);

        var maxBytes = validation.Kind == MediaKind.Image
            ? MediaConstants.MaxImageBytes
            : MediaConstants.MaxVideoBytes;

        var id = Guid.NewGuid();
        var storedFileName = $"{id:D}.{validation.Extension}";

        await using var combinedStream = new PrefixedReadStream(header, headerRead, content);
        var savedBytes = await _storage
            .SaveAsync(combinedStream, storedFileName, maxBytes, cancellationToken)
            .ConfigureAwait(false);

        var asset = MediaAsset.Create(id, originalFileName, validation, savedBytes);

        try
        {
            await _repository.CreateAsync(asset, cancellationToken).ConfigureAwait(false);
            return asset;
        }
        catch
        {
            await _storage.DeleteAsync(storedFileName, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public Task<IReadOnlyList<MediaAsset>> ListAllAsync(CancellationToken cancellationToken = default) =>
        _repository.ListAllAsync(cancellationToken);

    public async Task<LiveState> AssignBackgroundAsync(
        Guid mediaId,
        LiveStateManager liveStateManager,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(liveStateManager);

        var asset = await _repository.GetByIdAsync(mediaId, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException($"No se encontró el activo multimedia '{mediaId}'.");

        return await liveStateManager
            .SetBackgroundMediaAsync(new BackgroundMediaReference(asset.Id, asset.Kind), cancellationToken)
            .ConfigureAwait(false);
    }

    public static Task<LiveState> ClearBackgroundAsync(
        LiveStateManager liveStateManager,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(liveStateManager);
        return liveStateManager.SetBackgroundMediaAsync(null, cancellationToken);
    }

    public async Task<bool> DeleteAsync(
        Guid mediaId,
        LiveStateManager? liveStateManager = null,
        CancellationToken cancellationToken = default)
    {
        var asset = await _repository.GetByIdAsync(mediaId, cancellationToken).ConfigureAwait(false);
        if (asset is null)
        {
            return false;
        }

        var deleted = await _repository.DeleteAsync(mediaId, cancellationToken).ConfigureAwait(false);
        if (!deleted)
        {
            return false;
        }

        await _storage.DeleteAsync(asset.StoredFileName, cancellationToken).ConfigureAwait(false);

        if (liveStateManager is not null && liveStateManager.Snapshot().BackgroundMedia?.Id == mediaId)
        {
            await liveStateManager.SetBackgroundMediaAsync(null, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    private static async Task<int> ReadHeaderAsync(
        Stream stream,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(
                buffer.AsMemory(total, buffer.Length - total),
                cancellationToken).ConfigureAwait(false);

            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    private sealed class PrefixedReadStream : Stream
    {
        private readonly byte[] _prefix;
        private readonly int _prefixLength;
        private readonly Stream _inner;
        private int _prefixOffset;

        public PrefixedReadStream(byte[] prefix, int prefixLength, Stream inner)
        {
            _prefix = prefix;
            _prefixLength = prefixLength;
            _inner = inner;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_prefixOffset < _prefixLength)
            {
                var toCopy = Math.Min(count, _prefixLength - _prefixOffset);
                Buffer.BlockCopy(_prefix, _prefixOffset, buffer, offset, toCopy);
                _prefixOffset += toCopy;
                return toCopy;
            }

            return _inner.Read(buffer, offset, count);
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (_prefixOffset < _prefixLength)
            {
                var toCopy = Math.Min(buffer.Length, _prefixLength - _prefixOffset);
                _prefix.AsSpan(_prefixOffset, toCopy).CopyTo(buffer.Span);
                _prefixOffset += toCopy;
                return toCopy;
            }

            return await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
