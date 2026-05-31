using HtmlTinkerX;
using System;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>Parses common well-known text files such as security.txt, humans.txt, and ads.txt.</summary>
[Cmdlet(VerbsData.ConvertFrom, "WellKnownText", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(HtmlWellKnownRecord))]
public sealed class CmdletConvertFromWellKnownText : AsyncPSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetFile = "File";
    private const string ParameterSetUrl = "Url";

    /// <summary>Well-known text file content.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    [ValidateNotNullOrEmpty]
    public string Content { get; set; } = string.Empty;

    /// <summary>Path to a well-known text file.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFile)]
    [Alias("File")]
    [ValidateNotNullOrEmpty]
    public string Path { get; set; } = string.Empty;

    /// <summary>URL of a well-known text file to download and parse.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl)]
    [Alias("Uri")]
    public Uri Url { get; set; } = null!;

    /// <summary>Kind of well-known text file to parse.</summary>
    [Parameter(Mandatory = true)]
    [ValidateSet("SecurityTxt", "HumansTxt", "AdsTxt", "security.txt", "humans.txt", "ads.txt")]
    public string Kind { get; set; } = string.Empty;

    /// <summary>Base URL used to resolve relative security.txt URLs. Defaults to Url when downloading.</summary>
    [Parameter]
    public Uri? BaseUrl { get; set; }

    /// <summary>Proxy server address used when downloading by URL.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public string? Proxy { get; set; }

    /// <summary>Credentials used with the proxy server.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public PSCredential? ProxyCredential { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateProxy(Proxy, ProxyCredential);
        string content = await ReadContentAsync().ConfigureAwait(false);
        Uri? baseUri = BaseUrl ?? (ParameterSetName == ParameterSetUrl ? Url : null);
        WriteObject(HtmlWellKnownParser.Parse(content, NormalizeKind(Kind), baseUri).ToArray(), true);
    }

    private async Task<string> ReadContentAsync() {
        switch (ParameterSetName) {
            case ParameterSetFile:
                return await HtmlUtilities.ReadFileCheckedAsync(Path.ToFullPath()).ConfigureAwait(false);
            case ParameterSetUrl:
                using (HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential)) {
                    return await HtmlUtilities.GetStringWithProperEncodingAsync(client, Url.ToString()).ConfigureAwait(false);
                }
            default:
                return Content;
        }
    }

    private static string NormalizeKind(string kind) {
        return kind switch {
            "SecurityTxt" => "security.txt",
            "HumansTxt" => "humans.txt",
            "AdsTxt" => "ads.txt",
            _ => kind
        };
    }
}
