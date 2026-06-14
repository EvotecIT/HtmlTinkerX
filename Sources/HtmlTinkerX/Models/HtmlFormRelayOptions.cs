using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Options controlling browserless hidden-form relay processing.
/// </summary>
public sealed class HtmlFormRelayOptions {
    /// <summary>Maximum number of relay forms to submit before stopping.</summary>
    public int MaxRelayCount { get; set; } = 5;

    /// <summary>Allows relay form actions to post to a different host.</summary>
    public bool AllowCrossHost { get; set; }

    /// <summary>Optional host allow-list for cross-host relay actions.</summary>
    public IReadOnlyCollection<string> AllowedHosts { get; set; } = new List<string>();
}
