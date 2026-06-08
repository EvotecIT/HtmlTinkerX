using HtmlTinkerX;
using System;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>Extracts URL references from CSS declarations and imports.</summary>
/// <example>
///   <summary>Extract and resolve CSS URLs</summary>
///   <code>ConvertFrom-CssUrl -Content '.hero { background: url(/img/hero.png); }' -BaseUrl 'https://example.org/page/'</code>
/// </example>
[Cmdlet(VerbsData.ConvertFrom, "CssUrl", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(HtmlCssUrlReference))]
public sealed class CmdletConvertFromCssUrl : AsyncPSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetFile = "File";

    /// <summary>CSS content to inspect.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    [ValidateNotNullOrEmpty]
    public string Content { get; set; } = string.Empty;

    /// <summary>Path to a CSS file.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFile)]
    [Alias("File")]
    [ValidateNotNullOrEmpty]
    public string Path { get; set; } = string.Empty;

    /// <summary>Base URL used to resolve relative CSS URLs.</summary>
    [Parameter]
    public Uri? BaseUrl { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        string css = ParameterSetName == ParameterSetFile
            ? await HtmlUtilities.ReadFileCheckedAsync(Path.ToFullPath()).ConfigureAwait(false)
            : Content;

        WriteObject(HtmlCssQueryParser.SelectUrls(css, BaseUrl).ToArray(), true);
    }
}
