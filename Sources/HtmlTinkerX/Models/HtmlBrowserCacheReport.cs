using System;
using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>Machine-readable browser cache diagnosis. No browser is downloaded unless repair is requested.</summary>
public sealed class HtmlBrowserCacheReport {
    /// <summary>Selected cache root; managed generations live below its .htmltinkerx directory.</summary>
    public string CacheRoot { get; set; } = string.Empty;
    /// <summary>Protected generations, download state, and install history. Hermetic caches are outside the replaceable driver payload.</summary>
    public string ManagedRoot { get; set; } = string.Empty;
    /// <summary>Loaded Playwright driver version.</summary>
    public string PlaywrightVersion { get; set; } = string.Empty;
    /// <summary>Available bytes on the cache volume, or null when the volume cannot be queried.</summary>
    public long? AvailableBytes { get; set; }
    /// <summary>Current or last install lock owner.</summary>
    public HtmlBrowserInstallOwner? LockOwner { get; set; }
    /// <summary>Whether the OS-held install lock is occupied.</summary>
    public bool LockHeld { get; set; }
    /// <summary>Suggested retry interval while an install is running.</summary>
    public int RetryAfterSeconds { get; set; }
    /// <summary>0: requested runtimes verified; 2: cache requires repair; 3: repair in progress; 4: verified fallback only.</summary>
    public int ExitCode { get; set; }
    /// <summary>Browser and revision states, including missing expected revisions.</summary>
    public List<HtmlBrowserCacheRevision> Revisions { get; set; } = new();
    /// <summary>Last append-only installation records, as JSON lines.</summary>
    public List<string> RecentInstalls { get; set; } = new();
}

/// <summary>Attribution for the process holding the machine-wide installation lock.</summary>
public sealed class HtmlBrowserInstallOwner {
    /// <summary>Process identity, paired with its start time to distinguish PID reuse.</summary>
    public int ProcessId { get; set; }
    /// <summary>UTC process start time.</summary>
    public DateTime ProcessStartUtc { get; set; }
    /// <summary>UTC acquisition time.</summary>
    public DateTime AcquiredUtc { get; set; }
    /// <summary>Executable name. Arguments are omitted to avoid logging credentials.</summary>
    public string Command { get; set; } = string.Empty;
    /// <summary>Working directory of the installer.</summary>
    public string Repository { get; set; } = string.Empty;
    /// <summary>Cache being changed.</summary>
    public string CacheRoot { get; set; } = string.Empty;
}

/// <summary>Integrity and launch result for one browser revision.</summary>
public sealed class HtmlBrowserCacheRevision {
    /// <summary>Playwright executable name, including chromium-headless-shell when applicable.</summary>
    public string Browser { get; set; } = string.Empty;
    /// <summary>Playwright revision directory name.</summary>
    public string Revision { get; set; } = string.Empty;
    /// <summary>Verified, incomplete, missing, or unverified.</summary>
    public string State { get; set; } = string.Empty;
    /// <summary>Full revision directory.</summary>
    public string Directory { get; set; } = string.Empty;
    /// <summary>Diagnostic or smoke-test failure.</summary>
    public string Message { get; set; } = string.Empty;
}
