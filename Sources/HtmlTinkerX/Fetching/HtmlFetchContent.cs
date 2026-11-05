using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Keeps a response body's host slot until the body has been read to its end or the response is disposed: a site
/// streaming a large page slowly still has that request in flight after the headers arrive.
/// </summary>
internal sealed class HtmlLeasedContent : HttpContent {
    private readonly HttpContent _content;
    private IDisposable? _lease;

    internal HtmlLeasedContent(HttpContent content, IDisposable lease) {
        _content = content;
        _lease = lease;
        foreach (var header in content.Headers) Headers.TryAddWithoutValidation(header.Key, header.Value);
    }

    private void Release() => Interlocked.Exchange(ref _lease, null)?.Dispose();

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context) {
        await _content.CopyToAsync(stream).ConfigureAwait(false);
        Release();
    }

    protected override async Task<Stream> CreateContentReadStreamAsync() =>
        new EndNotifyingStream(await _content.ReadAsStreamAsync().ConfigureAwait(false), Release);

#if NET5_0_OR_GREATER
    // Without these, a caller's token never reaches a stalled body and the host slot is held until disposal.
    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken) {
        await _content.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
        Release();
    }

    protected override async Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken) =>
        new EndNotifyingStream(await _content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false), Release);
#endif

    protected override bool TryComputeLength(out long length) {
        length = _content.Headers.ContentLength ?? 0;
        return _content.Headers.ContentLength.HasValue;
    }

    protected override void Dispose(bool disposing) {
        try {
            if (disposing) _content.Dispose();
        } finally {
            try { if (disposing) Release(); }
            finally { base.Dispose(disposing); }
        }
    }

    private sealed class EndNotifyingStream(Stream inner, Action ended) : Stream {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) => Ended(inner.Read(buffer, offset, count), count);

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            Ended(await inner.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false), count);

#if NET5_0_OR_GREATER
        public override int Read(Span<byte> buffer) => Ended(inner.Read(buffer), buffer.Length);

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            Ended(await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false), buffer.Length);
#endif

        // A zero-length read is a probe, not the end of the body.
        private int Ended(int read, int requested) {
            if (read == 0 && requested > 0) ended();
            return read;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing) {
            if (disposing) {
                try { inner.Dispose(); } finally { ended(); }
            }
            base.Dispose(disposing);
        }
    }
}

/// <summary>
/// Hands a body to the caller as it arrives and stores it once the caller has read all of it, so the response's
/// headers are not held back while the body downloads. A body longer than the limit is not stored.
/// </summary>
internal sealed class HtmlStoringContent : HttpContent {
    private readonly HttpContent _content;
    private readonly int _maximumBytes;
    private readonly Func<byte[], CancellationToken, Task> _store;

    internal HtmlStoringContent(HttpContent content, int maximumBytes, Func<byte[], CancellationToken, Task> store) {
        _content = content;
        _maximumBytes = maximumBytes;
        _store = store;
        foreach (var header in content.Headers) Headers.TryAddWithoutValidation(header.Key, header.Value);
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
        SerializeAsync(stream, CancellationToken.None);

    protected override Task<Stream> CreateContentReadStreamAsync() => OpenAsync(CancellationToken.None);

#if NET5_0_OR_GREATER
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken) =>
        SerializeAsync(stream, cancellationToken);

    protected override Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken) => OpenAsync(cancellationToken);
#endif

    private async Task SerializeAsync(Stream destination, CancellationToken cancellationToken) {
        using Stream body = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await body.CopyToAsync(destination, 81920, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Stream> OpenAsync(CancellationToken cancellationToken) {
#if NET5_0_OR_GREATER
        Stream inner = await _content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#else
        Stream inner = await _content.ReadAsStreamAsync().ConfigureAwait(false);
#endif
        return new StoringStream(inner, _maximumBytes, _store);
    }

    protected override bool TryComputeLength(out long length) {
        length = _content.Headers.ContentLength ?? 0;
        return _content.Headers.ContentLength.HasValue;
    }

    protected override void Dispose(bool disposing) {
        if (disposing) _content.Dispose();
        base.Dispose(disposing);
    }

    private sealed class StoringStream(Stream inner, int maximumBytes, Func<byte[], CancellationToken, Task> store) : Stream {
        private MemoryStream? _copy = new();

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) {
            int read = inner.Read(buffer, offset, count);
            ObserveAsync(buffer, offset, read, count, CancellationToken.None).GetAwaiter().GetResult();
            return read;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
            int read = await inner.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
            await ObserveAsync(buffer, offset, read, count, cancellationToken).ConfigureAwait(false);
            return read;
        }

        private Task ObserveAsync(byte[] buffer, int offset, int read, int requested, CancellationToken cancellationToken) {
            if (_copy == null || read == 0 && requested == 0) return Task.CompletedTask;
            if (read > 0) {
                if (_copy.Length + read <= maximumBytes) {
                    _copy.Write(buffer, offset, read);
                } else {
                    _copy.Dispose();
                    _copy = null;
                }
                return Task.CompletedTask;
            }
            byte[] body = _copy.ToArray();
            _copy.Dispose();
            _copy = null;
            return StoreAsync(body, cancellationToken);
        }

        // The cache only saves a later download; failing to store must not fail the caller's read of a complete body.
        private async Task StoreAsync(byte[] body, CancellationToken cancellationToken) {
            try {
                await store(body, cancellationToken).ConfigureAwait(false);
            } catch (Exception error) when (error is not OperationCanceledException) {
            }
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing) {
            if (disposing) {
                inner.Dispose();
                _copy?.Dispose();
                _copy = null;
            }
            base.Dispose(disposing);
        }
    }
}

/// <summary>Replays bytes already inspected from a body, followed by the rest of the original stream.</summary>
internal sealed class HtmlReplayContent : HttpContent {
    private readonly HttpContent _original;
    private readonly byte[] _head;
    private readonly Stream? _rest;

    internal HtmlReplayContent(HttpContent original, byte[] head, Stream? rest) {
        _original = original;
        _head = head;
        _rest = rest;
        foreach (var header in original.Headers) Headers.TryAddWithoutValidation(header.Key, header.Value);
    }

    /// <summary>
    /// Reads up to <paramref name="maximumBytes"/> of a response body and replaces the content so the caller can still
    /// read all of it. Complete is false when the body is longer than the limit.
    /// </summary>
    internal static async Task<(byte[] Bytes, bool Complete)> BufferAsync(HttpResponseMessage response, int maximumBytes, CancellationToken cancellationToken) {
        HttpContent original = response.Content!;
#if NET5_0_OR_GREATER
        Stream stream = await original.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#else
        Stream stream = await original.ReadAsStreamAsync().ConfigureAwait(false);
#endif
        byte[] buffer = new byte[Math.Min(maximumBytes, 81920)];
        using MemoryStream head = new();
        bool ended = false;
        try {
            while (head.Length <= maximumBytes) {
                int read = await stream.ReadAsync(buffer, 0, (int)Math.Min(buffer.Length, maximumBytes + 1L - head.Length), cancellationToken).ConfigureAwait(false);
                if (read == 0) { ended = true; break; }
                head.Write(buffer, 0, read);
            }
        } catch {
            stream.Dispose();
            throw;
        }
        byte[] bytes = head.ToArray();
        if (ended) stream.Dispose();
        response.Content = new HtmlReplayContent(original, bytes, ended ? null : stream);
        return (bytes, ended);
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
        SerializeAsync(stream, CancellationToken.None);

    // Streams the rest instead of letting HttpContent buffer all of it, without the caller's token, before the first byte.
    protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new ReplayStream(_head, _rest));

#if NET5_0_OR_GREATER
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken) =>
        SerializeAsync(stream, cancellationToken);

    protected override Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken) => CreateContentReadStreamAsync();
#endif

    private async Task SerializeAsync(Stream stream, CancellationToken cancellationToken) {
        await stream.WriteAsync(_head, 0, _head.Length, cancellationToken).ConfigureAwait(false);
        if (_rest != null) await _rest.CopyToAsync(stream, 81920, cancellationToken).ConfigureAwait(false);
    }

    protected override bool TryComputeLength(out long length) {
        length = _rest == null ? _head.Length : 0;
        return _rest == null;
    }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            try { _rest?.Dispose(); } finally { _original.Dispose(); }
        }
        base.Dispose(disposing);
    }

    private sealed class ReplayStream(byte[] head, Stream? rest) : Stream {
        private int _position;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) {
            if (_position >= head.Length) return rest?.Read(buffer, offset, count) ?? 0;
            int copied = Math.Min(count, head.Length - _position);
            Buffer.BlockCopy(head, _position, buffer, offset, copied);
            _position += copied;
            return copied;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            return _position < head.Length || rest == null
                ? Task.FromResult(Read(buffer, offset, count))
                : rest.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing) {
            if (disposing) rest?.Dispose();
            base.Dispose(disposing);
        }
    }
}
