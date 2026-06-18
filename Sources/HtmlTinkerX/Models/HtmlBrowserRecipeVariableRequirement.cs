namespace HtmlTinkerX;

using System.Collections.Generic;

/// <summary>
/// Runtime variable required or accepted by a browser automation recipe.
/// </summary>
public sealed class HtmlBrowserRecipeVariableRequirement {
    /// <summary>Variable name used in recipe steps and in Invoke-HtmlBrowserRecipe -Variable.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Whether replay is expected to fail when the variable is not supplied.</summary>
    public bool Required { get; set; }

    /// <summary>Whether the caller supplied this variable during validation.</summary>
    public bool Supplied { get; set; }

    /// <summary>Whether the variable appears to contain or represent sensitive data.</summary>
    public bool Sensitive { get; set; }

    /// <summary>Human-readable reason this variable appears in the recipe.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Zero-based recipe step indexes where the variable is used.</summary>
    public List<int> StepIndexes { get; set; } = new();

    /// <summary>Recipe step names where the variable is used.</summary>
    public List<string> StepNames { get; set; } = new();

    /// <summary>Recipe step actions where the variable is used.</summary>
    public List<HtmlBrowserRecipeAction> Actions { get; set; } = new();

    /// <summary>Placeholder value suitable for template output.</summary>
    public string Placeholder { get; set; } = "<value>";
}
