namespace HtmlTinkerX;

/// <summary>
/// Describes intent-focused browser automation defaults for common admin proof and extraction workflows.
/// </summary>
public enum HtmlBrowserScenario {
    /// <summary>Do not apply any scenario defaults beyond the base browser options.</summary>
    Custom = 0,

    /// <summary>Optimize for repeatable screenshot, HTML, text, and manifest evidence packs.</summary>
    AuditProof = 1,

    /// <summary>Optimize for mailbox or SaaS proof pages that often need a stable viewport and rendered content readiness.</summary>
    MailboxProof = 2,

    /// <summary>Optimize for authenticated pages where a persistent or visible profile may be used.</summary>
    LoginProtected = 3,

    /// <summary>Optimize for JavaScript applications where DOM content may be ready before network requests fully settle.</summary>
    SinglePageApp = 4,

    /// <summary>Optimize for constrained environments by skipping heavier visual resources.</summary>
    LowBandwidth = 5,

    /// <summary>Optimize for workflows that inspect rendered output together with captured network activity.</summary>
    NetworkCapture = 6,

    /// <summary>Optimize for pages where visible proof and download-related evidence are collected together.</summary>
    DownloadEvidence = 7
}
