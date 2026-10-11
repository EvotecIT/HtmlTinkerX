using System;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

internal sealed class HtmlCrawlResponseBudget {
    private readonly string _optionName;
    private readonly long _limitBytes;
    private long _bytesRead;
    private bool _exceeded;
    private readonly object _state = new();
    private readonly SemaphoreSlim _readGate = new(1, 1);

    // Keep the remaining-byte calculation and the completed read in one reservation.
    internal Task EnterReadAsync(CancellationToken token) => _readGate.WaitAsync(token);
    internal void ExitRead() => _readGate.Release();

    internal HtmlCrawlResponseBudget(string optionName, long limitBytes) {
        _optionName = optionName;
        _limitBytes = limitBytes;
    }

    internal int GetReadSize(int requestedBytes) {
        lock (_state) {
            ThrowIfExceeded();
            long remaining = _limitBytes - _bytesRead;
            return remaining >= requestedBytes ? requestedBytes : (int)remaining + 1;
        }
    }

    internal void RecordBytes(int bytesRead) {
        lock (_state) {
            _exceeded |= bytesRead > _limitBytes - _bytesRead;
            _bytesRead = _bytesRead > long.MaxValue - bytesRead ? long.MaxValue : _bytesRead + bytesRead;
        }
    }

    internal void ThrowIfExceeded() {
        lock (_state) {
            if (_exceeded) {
                throw new HtmlCrawlBudgetExceededException(_optionName, _limitBytes, _bytesRead);
            }
        }
    }
}
