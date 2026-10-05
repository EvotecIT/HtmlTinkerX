using System;

namespace HtmlTinkerX;

/// <summary>Indicates that reading an HTTP response exceeded a crawl's aggregate byte limit.</summary>
public sealed class HtmlCrawlBudgetExceededException : InvalidOperationException {
    internal HtmlCrawlBudgetExceededException(string optionName, long limitBytes, long responseBytesRead)
        : base($"The crawl exceeded {optionName} ({limitBytes} bytes).") {
        OptionName = optionName;
        LimitBytes = limitBytes;
        ResponseBytesRead = responseBytesRead;
    }

    /// <summary>Name of the crawl option whose limit was exceeded.</summary>
    public string OptionName { get; }

    /// <summary>Configured aggregate response limit in bytes.</summary>
    public long LimitBytes { get; }

    /// <summary>Total bytes read in this invocation, including the byte that detected overflow.</summary>
    public long ResponseBytesRead { get; }
}
