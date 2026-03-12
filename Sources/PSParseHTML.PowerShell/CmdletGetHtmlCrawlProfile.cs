using HtmlTinkerX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Returns built-in or custom crawl profiles.
/// </summary>
/// <example>
/// <code>Get-HtmlCrawlProfile</code>
/// </example>
/// <example>
/// <code>Get-HtmlCrawlProfile -Path .\crawl-profiles.json -Name custom-docs</code>
/// </example>
[Cmdlet(VerbsCommon.Get, "HtmlCrawlProfile")]
[OutputType(typeof(HtmlCrawlProfile))]
public sealed class CmdletGetHtmlCrawlProfile : AsyncPSCmdlet {
    /// <summary>Optional profile name filter.</summary>
    [Parameter(Position = 0)]
    public string[] Name { get; set; } = Array.Empty<string>();

    /// <summary>Optional JSON file containing custom crawl profiles.</summary>
    [Parameter]
    public string? Path { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        IReadOnlyList<HtmlCrawlProfile> profiles = string.IsNullOrWhiteSpace(Path)
            ? HtmlCrawlProfiles.Defaults
            : await HtmlCrawlProfiles.LoadFromPathAsync(Path!.ToFullPath(), CancelToken).ConfigureAwait(false);

        IEnumerable<HtmlCrawlProfile> filtered = profiles;
        if (Name.Length > 0) {
            HashSet<string> names = new(Name.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()), StringComparer.OrdinalIgnoreCase);
            filtered = filtered.Where(profile => names.Contains(profile.Name));
        }

        WriteObject(filtered.ToArray(), true);
    }
}
