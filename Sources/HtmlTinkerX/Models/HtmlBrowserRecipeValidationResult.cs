using System.Collections.Generic;
using System.Linq;

namespace HtmlTinkerX;

/// <summary>
/// Preflight validation result for a browser automation recipe.
/// </summary>
public sealed class HtmlBrowserRecipeValidationResult {
    /// <summary>Recipe name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Whether warnings are treated as blocking issues for this validation result.</summary>
    public bool StrictPreflight { get; set; }

    /// <summary>Whether the recipe has no validation errors.</summary>
    public bool IsValid => ErrorCount == 0;

    /// <summary>Whether the recipe passed validation for the configured preflight mode.</summary>
    public bool Passed => !HasBlockingIssues(StrictPreflight);

    /// <summary>Whether the recipe has no errors and, optionally, no warnings.</summary>
    /// <param name="treatWarningsAsErrors">When true, warnings are treated as blocking issues.</param>
    /// <returns>True when validation has no blocking issues.</returns>
    public bool IsValidForReplay(bool treatWarningsAsErrors) => !HasBlockingIssues(treatWarningsAsErrors);

    /// <summary>Whether validation contains issues that should block replay for the chosen preflight mode.</summary>
    /// <param name="treatWarningsAsErrors">When true, warnings are treated as blocking issues.</param>
    /// <returns>True when replay should stop before browser execution.</returns>
    public bool HasBlockingIssues(bool treatWarningsAsErrors) =>
        ErrorCount > 0 || (treatWarningsAsErrors && WarningCount > 0);

    /// <summary>Number of validation errors.</summary>
    public int ErrorCount => Issues.Count(issue => issue.Severity == HtmlBrowserRecipeValidationSeverity.Error);

    /// <summary>Number of validation warnings.</summary>
    public int WarningCount => Issues.Count(issue => issue.Severity == HtmlBrowserRecipeValidationSeverity.Warning);

    /// <summary>Total number of validation issues.</summary>
    public int IssueCount => Issues.Count;

    /// <summary>Number of issues that block replay for the configured preflight mode.</summary>
    public int BlockingIssueCount => BlockingIssues.Length;

    /// <summary>Validation issues that block replay for the configured preflight mode.</summary>
    public HtmlBrowserRecipeValidationIssue[] BlockingIssues => Issues
        .Where(issue => IsBlockingIssue(issue, StrictPreflight))
        .ToArray();

    /// <summary>Recommended process exit code for CI scripts that validate this recipe.</summary>
    public int RecommendedExitCode => Passed ? 0 : 1;

    /// <summary>Validated step count.</summary>
    public int StepCount { get; set; }

    /// <summary>Runtime variables discovered from recipe steps.</summary>
    public List<HtmlBrowserRecipeVariableRequirement> Variables { get; set; } = new();

    /// <summary>Names of runtime variables that are expected before replay.</summary>
    public string[] RequiredVariables => Variables
        .Where(variable => variable.Required)
        .Select(variable => variable.Name)
        .ToArray();

    /// <summary>Names of required runtime variables that were not supplied during validation.</summary>
    public string[] MissingVariables => Variables
        .Where(variable => variable.Required && !variable.Supplied)
        .Select(variable => variable.Name)
        .ToArray();

    /// <summary>Number of required runtime variables.</summary>
    public int RequiredVariableCount => RequiredVariables.Length;

    /// <summary>Number of required runtime variables not supplied during validation.</summary>
    public int MissingVariableCount => MissingVariables.Length;

    /// <summary>Hashtable-style template values for Invoke-HtmlBrowserRecipe -Variable.</summary>
    public Dictionary<string, string> VariableTemplate => Variables
        .Where(variable => variable.Required)
        .ToDictionary(variable => variable.Name, variable => variable.Placeholder, System.StringComparer.OrdinalIgnoreCase);

    /// <summary>Validation issues.</summary>
    public List<HtmlBrowserRecipeValidationIssue> Issues { get; set; } = new();

    /// <summary>Suggested next command after validation.</summary>
    public string SuggestedCommand { get; set; } = string.Empty;

    /// <summary>Short validation summary suitable for logs and CI output.</summary>
    public string Summary {
        get {
            string mode = StrictPreflight ? "strict preflight" : "preflight";
            string state = Passed ? "passed" : "failed";
            return $"Recipe {mode} {state} with {ErrorCount} error(s), {WarningCount} warning(s), and {BlockingIssueCount} blocking issue(s).";
        }
    }

    private static bool IsBlockingIssue(HtmlBrowserRecipeValidationIssue issue, bool treatWarningsAsErrors) =>
        issue.Severity == HtmlBrowserRecipeValidationSeverity.Error
        || (treatWarningsAsErrors && issue.Severity == HtmlBrowserRecipeValidationSeverity.Warning);
}
