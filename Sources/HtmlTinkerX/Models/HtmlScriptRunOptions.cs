using System;
using System.Threading;

namespace HtmlTinkerX;

/// <summary>Bounds JavaScript execution in an owned HTML browsing context.</summary>
public sealed class HtmlScriptRunOptions {
    /// <summary>Default maximum number of statements in one script execution.</summary>
    public const int DefaultMaximumStatements = 1_000_000;
    /// <summary>Default managed allocation budget for one script execution, in bytes.</summary>
    public const long DefaultMaximumMemoryBytes = 64L * 1024 * 1024;
    /// <summary>Default maximum number of characters in the supplied HTML.</summary>
    public const int DefaultMaximumHtmlCharacters = 16 * 1024 * 1024;
    /// <summary>Default maximum number of characters in the requested script.</summary>
    public const int DefaultMaximumScriptCharacters = 1024 * 1024;

    /// <summary>Whole-operation timeout, including page loading and the requested script. Defaults to ten seconds. Use <see cref="Timeout.InfiniteTimeSpan"/> for cancellation-only timing.</summary>
    public TimeSpan ExecutionTimeout { get; set; } = TimeSpan.FromSeconds(10);
    /// <summary>Statement limit for each page or requested script execution. Must be positive.</summary>
    public int MaximumStatements { get; set; } = DefaultMaximumStatements;
    /// <summary>Jint's managed allocation limit for each script execution. This is not a process or DOM size limit. Must be positive.</summary>
    public long MaximumMemoryBytes { get; set; } = DefaultMaximumMemoryBytes;
    /// <summary>Maximum HTML input length. Must be positive.</summary>
    public int MaximumHtmlCharacters { get; set; } = DefaultMaximumHtmlCharacters;
    /// <summary>Maximum requested script length. Must be positive.</summary>
    public int MaximumScriptCharacters { get; set; } = DefaultMaximumScriptCharacters;
    /// <summary>Whether page scripts and event handlers run. Defaults to true. When false, only the explicitly requested script is evaluated.</summary>
    public bool ExecutePageScripts { get; set; } = true;

    internal HtmlScriptRunOptions Snapshot() {
        if (ExecutionTimeout != Timeout.InfiniteTimeSpan &&
            (ExecutionTimeout <= TimeSpan.Zero || ExecutionTimeout.TotalMilliseconds > int.MaxValue)) {
            throw new ArgumentOutOfRangeException(nameof(ExecutionTimeout), "The timeout must be positive and at most Int32.MaxValue milliseconds, or infinite.");
        }
        if (MaximumStatements <= 0) throw new ArgumentOutOfRangeException(nameof(MaximumStatements));
        if (MaximumMemoryBytes <= 0) throw new ArgumentOutOfRangeException(nameof(MaximumMemoryBytes));
        if (MaximumHtmlCharacters <= 0) throw new ArgumentOutOfRangeException(nameof(MaximumHtmlCharacters));
        if (MaximumScriptCharacters <= 0) throw new ArgumentOutOfRangeException(nameof(MaximumScriptCharacters));
        return new HtmlScriptRunOptions {
            ExecutionTimeout = ExecutionTimeout, MaximumStatements = MaximumStatements,
            MaximumMemoryBytes = MaximumMemoryBytes, MaximumHtmlCharacters = MaximumHtmlCharacters,
            MaximumScriptCharacters = MaximumScriptCharacters, ExecutePageScripts = ExecutePageScripts
        };
    }
}
