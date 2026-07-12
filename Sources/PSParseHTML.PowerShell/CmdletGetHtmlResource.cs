using HtmlTinkerX;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Parses external and inline resources and returns their links or downloads them.
/// </summary>
[Cmdlet(VerbsCommon.Get, "HtmlResource", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(HtmlResourceLink))]
public sealed class CmdletGetHtmlResource : AsyncPSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetFile = "File";
    private const string ParameterSetUrl = "Url";

    /// <summary>HTML content to parse.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    public string Content { get; set; } = string.Empty;

    /// <summary>Path to an HTML file.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFile)]
    [Alias("File")]
    public string Path { get; set; } = string.Empty;

    /// <summary>URL of an HTML page.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl)]
    [Alias("Uri")]
    public Uri Url { get; set; } = null!;

    /// <summary>Proxy server address used when downloading by URL.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public string? Proxy { get; set; }

    /// <summary>Credentials for the specified proxy server.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public PSCredential? ProxyCredential { get; set; }

    /// <summary>Directory where scripts will be saved when <see cref="Download"/> is specified.</summary>
    [Parameter]
    public string? OutDirectory { get; set; }

    /// <summary>Download the scripts instead of returning URLs. Only valid with <c>-Url</c>.</summary>
    [Parameter]
    public SwitchParameter Download { get; set; }

    /// <summary>Include CSS &lt;link&gt; and &lt;style&gt; tags.</summary>
    [Parameter]
    public SwitchParameter IncludeCss { get; set; }

    /// <summary>Include inline &lt;script&gt; or &lt;style&gt; content.</summary>
    [Parameter]
    public SwitchParameter IncludeInline { get; set; }

    /// <summary>Return the content of external resources.</summary>
    [Parameter]
    public SwitchParameter AsContent { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateProxy(Proxy, ProxyCredential);
        List<HtmlResourceLink> links;
        HttpClient? client = null;
        switch (ParameterSetName) {
            case ParameterSetUrl:
                client = HttpClientHelper.Create(Proxy, ProxyCredential);
                links = await HtmlResourceParser.ParseUrlAsync(Url.ToString(), IncludeCss.IsPresent, IncludeInline.IsPresent, client, cancellationToken: CancelToken).ConfigureAwait(false);
                break;
            case ParameterSetFile:
                string html = await HtmlUtilities.ReadFileCheckedAsync(Path, CancelToken).ConfigureAwait(false);
                links = HtmlResourceParser.Parse(html, IncludeCss.IsPresent, IncludeInline.IsPresent);
                break;
            default:
                links = HtmlResourceParser.Parse(Content, IncludeCss.IsPresent, IncludeInline.IsPresent);
                break;
        }

        if (AsContent.IsPresent) {
            HttpClient http = client ?? HtmlHttpClientFactory.Shared;
            foreach (var link in links) {
                if (string.IsNullOrEmpty(link.Content) && !string.IsNullOrEmpty(link.Source)) {
                    Uri srcUri = ParameterSetName switch {
                        ParameterSetUrl => new Uri(Url, link.Source),
                        ParameterSetFile => new Uri(new Uri(System.IO.Path.GetDirectoryName(Path)! + System.IO.Path.DirectorySeparatorChar), link.Source),
                        _ => new Uri(link.Source, UriKind.RelativeOrAbsolute)
                    };
                    if (srcUri.IsFile) {
#if NETSTANDARD2_0 || NETFRAMEWORK
                        link.Content = File.ReadAllText(srcUri.LocalPath);
#else
                        link.Content = await File.ReadAllTextAsync(srcUri.LocalPath).ConfigureAwait(false);
#endif
                    } else {
                        link.Content = await HtmlUtilities.GetStringWithProperEncodingAsync(http, srcUri.ToString(), CancelToken).ConfigureAwait(false);
                    }
                }
            }
        }

        if (Download.IsPresent) {
            if (ParameterSetName != ParameterSetUrl) {
                ThrowTerminatingError(new ErrorRecord(new PSInvalidOperationException("-Download is only supported when -Url is used."), "InvalidParameter", ErrorCategory.InvalidArgument, null));
                return;
            }
            if (string.IsNullOrEmpty(OutDirectory)) {
                ThrowTerminatingError(new ErrorRecord(new PSArgumentNullException(nameof(OutDirectory)), "MissingOutDirectory", ErrorCategory.InvalidArgument, null));
                return;
            }
            var paths = new List<string>();
            foreach (var link in links) {
                paths.Add(await link.SaveAsync(OutDirectory!, Url, client!, cancellationToken: CancelToken).ConfigureAwait(false));
            }
            WriteObject(paths.ToArray(), true);
        } else {
            WriteObject(links.ToArray(), true);
        }
        client?.Dispose();
    }
}
