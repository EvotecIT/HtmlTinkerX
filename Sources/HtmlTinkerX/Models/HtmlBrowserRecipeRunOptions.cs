using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Options used when executing a browser recipe.
/// </summary>
public sealed class HtmlBrowserRecipeRunOptions {
    /// <summary>
    /// Browser launch options used when the recipe creates its own session. Ignored when an existing session is supplied.
    /// </summary>
    public HtmlBrowserLaunchOptions? LaunchOptions { get; set; }

    /// <summary>
    /// Runtime values used to replace redacted or parameterized recipe step values.
    /// </summary>
    public Dictionary<string, string> Variables { get; } = new(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Export failure evidence when a recipe step fails for this run, without changing the saved recipe definition.
    /// </summary>
    public bool OnFailureEvidence { get; set; }

    /// <summary>
    /// Root folder for runtime-requested failure evidence.
    /// </summary>
    public string? FailureEvidenceFolder { get; set; }
}
