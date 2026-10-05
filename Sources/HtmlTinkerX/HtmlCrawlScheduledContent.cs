using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>Preserves the transport content and releases its origin slot when the response closes.</summary>
internal sealed class HtmlCrawlScheduledContent : HttpContent {
    private readonly HttpContent _content;
    private IDisposable? _lease;

    internal HtmlCrawlScheduledContent(HttpContent content, IDisposable lease) {
        _content = content;
        _lease = lease;
        foreach (var header in content.Headers) Headers.TryAddWithoutValidation(header.Key, header.Value);
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => _content.CopyToAsync(stream);
    protected override Task<Stream> CreateContentReadStreamAsync() => _content.ReadAsStreamAsync();
    protected override bool TryComputeLength(out long length) {
        length = _content.Headers.ContentLength ?? 0;
        return _content.Headers.ContentLength.HasValue;
    }

    protected override void Dispose(bool disposing) {
        try {
            if (disposing) _content.Dispose();
        } finally {
            try { if (disposing) Interlocked.Exchange(ref _lease, null)?.Dispose(); }
            finally { base.Dispose(disposing); }
        }
    }
}
