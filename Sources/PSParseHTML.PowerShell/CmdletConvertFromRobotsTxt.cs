using HtmlTinkerX;
using System;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>Parses robots.txt directives into structured rules.</summary>
[Cmdlet(VerbsData.ConvertFrom, "RobotsTxt", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(HtmlRobotsRule))]
public sealed class CmdletConvertFromRobotsTxt : AsyncUrlCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetFile = "File";
    private const string ParameterSetUrl = "Url";

    /// <summary>Robots.txt content to parse.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    [ValidateNotNullOrEmpty]
    public string Content { get; set; } = string.Empty;

    /// <summary>Path to a robots.txt file.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFile)]
    [Alias("File")]
    [ValidateNotNullOrEmpty]
    public string Path { get; set; } = string.Empty;

    /// <summary>URL of a robots.txt document to download and parse.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl)]
    [Alias("Uri")]
    public Uri Url { get; set; } = null!;

    /// <summary>Base URL used to resolve sitemap URLs. Defaults to Url when downloading.</summary>
    [Parameter]
    public Uri? BaseUrl { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateUrlParameters();
        string content = await ReadContentAsync().ConfigureAwait(false);
        Uri? baseUri = BaseUrl ?? (ParameterSetName == ParameterSetUrl ? Url : null);
        WriteObject(HtmlRobotsParser.Parse(content, baseUri).ToArray(), true);
    }

    private async Task<string> ReadContentAsync() {
        switch (ParameterSetName) {
            case ParameterSetFile:
                return await HtmlUtilities.ReadFileCheckedAsync(Path.ToFullPath()).ConfigureAwait(false);
            case ParameterSetUrl:
                using (HttpClient client = await CreateUrlClientAsync().ConfigureAwait(false)) {
                    return await HtmlUtilities.GetStringWithProperEncodingAsync(client, Url.ToString(), fetchOptions: null, cancellationToken: CancelToken).ConfigureAwait(false);
                }
            default:
                return Content;
        }
    }
}
