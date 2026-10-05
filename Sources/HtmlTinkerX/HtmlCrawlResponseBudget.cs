using System;

namespace HtmlTinkerX;

internal sealed class HtmlCrawlResponseBudget {
    private readonly string _optionName;
    private readonly long _limitBytes;
    private long _bytesRead;
    private bool _exceeded;

    internal HtmlCrawlResponseBudget(string optionName, long limitBytes) {
        _optionName = optionName;
        _limitBytes = limitBytes;
    }

    internal int GetReadSize(int requestedBytes) {
        ThrowIfExceeded();
        long remaining = _limitBytes - _bytesRead;
        return remaining >= requestedBytes ? requestedBytes : (int)remaining + 1;
    }

    internal void RecordBytes(int bytesRead) {
        _exceeded |= bytesRead > _limitBytes - _bytesRead;
        _bytesRead = _bytesRead > long.MaxValue - bytesRead ? long.MaxValue : _bytesRead + bytesRead;
    }

    internal void ThrowIfExceeded() {
        if (_exceeded) {
            throw new HtmlCrawlBudgetExceededException(_optionName, _limitBytes, _bytesRead);
        }
    }
}
