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

        public override int Read(byte[] buffer, int offset, int count) => Ended(inner.Read(buffer, offset, count));

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            Ended(await inner.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false));

#if NET5_0_OR_GREATER
        public override int Read(Span<byte> buffer) => Ended(inner.Read(buffer));

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            Ended(await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false));
#endif

        private int Ended(int read) {
            if (read == 0) ended();
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
        Stream stream = await original.ReadAsStreamAsync().ConfigureAwait(false);
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

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context) {
        await stream.WriteAsync(_head, 0, _head.Length).ConfigureAwait(false);
        if (_rest != null) await _rest.CopyToAsync(stream).ConfigureAwait(false);
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
}
