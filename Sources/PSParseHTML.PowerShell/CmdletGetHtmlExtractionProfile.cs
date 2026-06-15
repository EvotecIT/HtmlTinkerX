using HtmlTinkerX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Returns built-in extraction workflow profiles or the profile recommended by an extraction plan.
/// </summary>
/// <example>
/// <code>Get-HtmlExtractionProfile</code>
/// </example>
/// <example>
/// <code>Test-HtmlExtractionPlan -Url https://example.com | Get-HtmlExtractionProfile</code>
/// </example>
[Cmdlet(VerbsCommon.Get, "HtmlExtractionProfile", DefaultParameterSetName = ParameterSetList)]
[OutputType(typeof(HtmlExtractionProfile))]
public sealed class CmdletGetHtmlExtractionProfile : AsyncPSCmdlet {
    private const string ParameterSetList = "List";
    private const string ParameterSetPlan = "Plan";

    /// <summary>Optional extraction profile name filter.</summary>
    [Parameter(Position = 0, ParameterSetName = ParameterSetList)]
    public string[] Name { get; set; } = Array.Empty<string>();

    /// <summary>Optional extraction mode filter.</summary>
    [Parameter(ParameterSetName = ParameterSetList)]
    public HtmlExtractionPlanMode? RecommendedMode { get; set; }

    /// <summary>Extraction plan whose suggested profile should be returned.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetPlan, ValueFromPipeline = true, Position = 0)]
    public HtmlExtractionPlan? Plan { get; set; }

    /// <inheritdoc />
    protected override Task ProcessRecordAsync() {
        IEnumerable<HtmlExtractionProfile> profiles = GetProfiles();
        WriteObject(profiles.ToArray(), true);
        return Task.CompletedTask;
    }

    private IEnumerable<HtmlExtractionProfile> GetProfiles() {
        if (ParameterSetName == ParameterSetPlan) {
            if (!string.IsNullOrWhiteSpace(Plan!.SuggestedProfileName)) {
                HtmlExtractionProfile? suggested = HtmlExtractionProfiles.ResolveByName(Plan.SuggestedProfileName);
                if (suggested != null) {
                    return new[] { suggested };
                }
            }

            return new[] { HtmlExtractionProfiles.Recommend(Plan!) };
        }

        IEnumerable<HtmlExtractionProfile> profiles = HtmlExtractionProfiles.Defaults;
        if (Name.Length > 0) {
            HashSet<string> names = new(Name.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()), StringComparer.OrdinalIgnoreCase);
            profiles = profiles.Where(profile => names.Contains(profile.Name));
        }

        if (RecommendedMode.HasValue) {
            profiles = profiles.Where(profile => profile.RecommendedMode == RecommendedMode.Value);
        }

        return profiles;
    }
}
