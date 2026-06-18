namespace HtmlTinkerX;

/// <summary>
/// Controls how SSO form and URL callback handoffs are inspected and returned.
/// </summary>
public sealed class HtmlBrowserSsoHandoffOptions {
    /// <summary>Include sensitive assertion, token, and state values in output. The default is redacted output.</summary>
    public bool IncludeSensitiveValues { get; set; }

    /// <summary>Return all forms, not only forms containing recognizable SSO handoff fields. URL handoffs still require recognizable protocol fields.</summary>
    public bool IncludeAllForms { get; set; }

    /// <summary>Maximum field value length to return. Zero disables truncation.</summary>
    public int MaxValueLength { get; set; } = 131072;

    /// <summary>Wait until at least one matching handoff form or URL callback is observed.</summary>
    public bool Wait { get; set; }

    /// <summary>Maximum time in milliseconds to wait for a handoff form or URL callback. Zero waits indefinitely.</summary>
    public int Timeout { get; set; } = 30000;

    /// <summary>Polling interval in milliseconds while waiting for a handoff form.</summary>
    public int PollMilliseconds { get; set; } = 250;
}
