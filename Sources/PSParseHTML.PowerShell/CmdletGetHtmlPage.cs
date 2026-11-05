using HtmlTinkerX;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>Reads an HTML page as headings, paragraphs, tables, links, resources, and inferred object collections.</summary>
/// <example>
///   <summary>Read a web page without writing selectors</summary>
///   <code>$page = Get-HtmlPage -Url https://example.org; $page.Headings; $page.Collections</code>
/// </example>
/// <example>
///   <summary>Read a page behind a CDN, rendering it in a browser only when the CDN blocks the request</summary>
///   <code>Get-HtmlPage -Url https://support.microsoft.com/kb/5127692 -BrowserHeaders -RetryCount 2 -RenderWhenBlocked</code>
/// </example>
/// <example>
///   <summary>Report why a page was refused</summary>
///   <code>try { Get-HtmlPage -Url $url } catch { $_.TargetObject.Kind; $_.TargetObject.Provider; $_.TargetObject.Excerpt }</code>
/// </example>
/// <example>
///   <summary>Inspect the best inferred repeated collection as PowerShell objects</summary>
///   <code>$page = Get-HtmlPage -Content $html; $page.Collections[0].Items</code>
/// </example>
[Cmdlet(VerbsCommon.Get, "HtmlPage", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(PSObject))]
public sealed class CmdletGetHtmlPage : AsyncPSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetFile = "File";
    private const string ParameterSetSnapshot = "Snapshot";
    private const string ParameterSetUrl = "Url";
    private Uri? _effectiveUrl;

    /// <summary>HTML content to read.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, Position = 0)]
    public string Content { get; set; } = string.Empty;

    /// <summary>Path to an HTML file to read.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFile, Position = 0)]
    [Alias("File")]
    public string Path { get; set; } = string.Empty;

    /// <summary>URL of an HTML page to download and read.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl, Position = 0)]
    [Alias("Uri")]
    public Uri Url { get; set; } = null!;

    /// <summary>Base URL used to resolve relative links and resources.</summary>
    [Parameter(ParameterSetName = ParameterSetContent)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public Uri? BaseUrl { get; set; }

    /// <summary>Optional plain-text hint used to focus repeated-collection discovery.</summary>
    [Parameter]
    [Alias("Query")]
    public string? CollectionHint { get; set; }

    /// <summary>Minimum number of repeated elements required for an inferred collection.</summary>
    [Parameter]
    [ValidateRange(2, int.MaxValue)]
    public int MinimumRepeatCount { get; set; } = 2;

    /// <summary>Maximum number of distinct inferred collections.</summary>
    [Parameter]
    [ValidateRange(1, 100)]
    public int CollectionLimit { get; set; } = 5;

    /// <summary>Skips repeated-collection inference.</summary>
    [Parameter]
    public SwitchParameter NoCollections { get; set; }

    /// <summary>Skips readable-text analysis and its title fallback. Semantic content remains available.</summary>
    [Parameter]
    public SwitchParameter NoReadableText { get; set; }

    /// <summary>Skips Markdown conversion. Semantic content remains available.</summary>
    [Parameter]
    public SwitchParameter NoMarkdown { get; set; }

    /// <summary>Skips normalized links, forms and assets. Semantic resources and collection inference are independent.</summary>
    [Parameter]
    public SwitchParameter NoWebData { get; set; }

    /// <summary>Rendered browser snapshot to read instead of the static source HTML.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSnapshot, Position = 0)]
    public HtmlRenderedPageSnapshot? RenderedSnapshot { get; set; }

    /// <summary>Proxy server address used when downloading by URL. Give several to send each attempt through the next one.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public string[]? Proxy { get; set; }

    /// <summary>Credentials used with the proxy server.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public PSCredential? ProxyCredential { get; set; }

    /// <summary>User-Agent header used when downloading <see cref="Url"/>.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public string? UserAgent { get; set; }

    /// <summary>Additional or replacement HTTP headers used when downloading <see cref="Url"/>.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [Alias("Headers")]
    public Hashtable? Header { get; set; }

    /// <summary>Retries after a transient status (408, 429, 500, 502, 503, 504) or a dropped connection, honouring Retry-After up to 30 seconds. Defaults to 1 with BrowserHeaders, RenderWhenBlocked or several proxies, otherwise 0.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    [ValidateRange(0, 10)]
    public int RetryCount { get; set; }

    /// <summary>Sends a consistent set of desktop Chrome headers: User-Agent, Accept, Accept-Language and client hints. Cannot be combined with UserAgent, or with Header values for the headers it sets.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public SwitchParameter BrowserHeaders { get; set; }

    /// <summary>Renders the page in a headless browser, through the first proxy, when a Cloudflare challenge or a recognised CDN block page is returned.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public SwitchParameter RenderWhenBlocked { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateProxy(FirstProxy, ProxyCredential);
        if (BrowserHeaders.IsPresent && !string.IsNullOrWhiteSpace(UserAgent)) {
            ThrowTerminatingError(new ErrorRecord(
                new PSArgumentException("BrowserHeaders sets its own consistent User-Agent; do not combine it with UserAgent."),
                "BrowserHeadersWithUserAgent", ErrorCategory.InvalidArgument, UserAgent));
        }
        if (BrowserHeaders.IsPresent && Header?.Keys.Cast<object>().Select(static key => key?.ToString()).FirstOrDefault(IsBrowserProfileHeader) is { } header) {
            ThrowTerminatingError(new ErrorRecord(
                new PSArgumentException($"BrowserHeaders sets its own consistent {header} and client hints; do not override them with Header."),
                "BrowserHeadersWithHeader", ErrorCategory.InvalidArgument, header));
        }
        string staticHtml = await ReadHtmlAsync().ConfigureAwait(false);
        if (ParameterSetName == ParameterSetSnapshot && string.IsNullOrWhiteSpace(RenderedSnapshot?.Html)) {
            throw new ArgumentException("RenderedSnapshot must contain HTML.", nameof(RenderedSnapshot));
        }

        bool useRendered = RenderedSnapshot != null && !string.IsNullOrWhiteSpace(RenderedSnapshot.Html);
        string html = useRendered ? RenderedSnapshot!.Html : staticHtml;
        Uri? snapshotUri = ParseAbsoluteUri(RenderedSnapshot?.Url);
        Uri? sourceUri = ParameterSetName == ParameterSetUrl
            ? Url
            : ParameterSetName == ParameterSetSnapshot ? snapshotUri : BaseUrl;
        Uri? finalUri = useRendered
            ? ParseAbsoluteUri(RenderedSnapshot!.FinalUrl) ?? ParseAbsoluteUri(RenderedSnapshot.Url) ?? _effectiveUrl ?? sourceUri
            : _effectiveUrl ?? sourceUri;

        HtmlPageDocument document = HtmlPageReader.Read(
            html,
            new HtmlPageReaderOptions {
                SourceUri = sourceUri,
                FinalUri = finalUri,
                BaseUri = finalUri ?? sourceUri,
                AnalysisMode = useRendered ? "RenderedSnapshot" : "Static",
                CollectionHint = CollectionHint,
                MinimumRepeatCount = MinimumRepeatCount,
                CollectionLimit = CollectionLimit,
                IncludeCollections = !NoCollections.IsPresent,
                IncludeReadableText = !NoReadableText.IsPresent,
                IncludeMarkdown = !NoMarkdown.IsPresent,
                IncludeWebData = !NoWebData.IsPresent
            });

        WriteObject(ProjectDocument(document));
    }

    private async Task<string> ReadHtmlAsync() {
        if (ParameterSetName == ParameterSetSnapshot) {
            return string.Empty;
        }

        if (ParameterSetName == ParameterSetFile) {
            return await HtmlUtilities.ReadFileCheckedAsync(Path.ToFullPath()).ConfigureAwait(false);
        }

        if (ParameterSetName == ParameterSetUrl) {
            using HttpClient client = CreateUrlClient();
            HtmlHttpTextResult result;
            try {
                result = await HtmlUtilities.GetTextWithProperEncodingAsync(
                    client,
                    Url.ToString(),
                    fetchOptions: null,
                    cancellationToken: CancelToken).ConfigureAwait(false);
            } catch (HtmlHttpException exception) {
                ThrowTerminatingError(new ErrorRecord(exception, "HtmlHttp" + exception.Kind, GetErrorCategory(exception.Kind), exception.Failure));
                throw;
            }
            _effectiveUrl = result.FinalUri ?? Url;
            return result.Content;
        }

        return Content;
    }

    private string? FirstProxy => Proxy?.FirstOrDefault(static proxy => !string.IsNullOrWhiteSpace(proxy));

    // Headers the browser profile keeps consistent with each other, including client hints it does not send.
    private static bool IsBrowserProfileHeader(string? name) => name != null
        && (HtmlBrowserHeaderProfile.Chrome().Headers.Any(header => header.Key.Equals(name, StringComparison.OrdinalIgnoreCase))
            || name.StartsWith("sec-ch-ua", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Sec-Fetch-", StringComparison.OrdinalIgnoreCase));

    private HttpClient CreateUrlClient() {
        bool fetchPolicy = MyInvocation.BoundParameters.ContainsKey(nameof(RetryCount)) || BrowserHeaders.IsPresent
            || RenderWhenBlocked.IsPresent || Proxy?.Length > 1;
        if (!fetchPolicy) return HttpClientHelper.Create(Proxy?.FirstOrDefault(), ProxyCredential, UserAgent, Header);

        HtmlFetchPolicy policy = new() {
            RetryCount = MyInvocation.BoundParameters.ContainsKey(nameof(RetryCount)) ? RetryCount : 1,
            BrowserHeaders = BrowserHeaders.IsPresent ? HtmlBrowserHeaderProfile.Chrome() : null,
            OnRetry = (host, reason) => WriteVerbose($"Retrying {host}: {reason}")
        };
        if (RenderWhenBlocked.IsPresent) {
            // The browser leaves through the same proxy as the request it replaces, never directly.
            NetworkCredential? proxyCredential = FirstProxy != null
                ? ProxyCredential?.GetNetworkCredential()
                : HtmlHttpClientFactory.DefaultProxyCredential as NetworkCredential;
            policy.Renderer = HtmlBrowser.CreateFetchRenderer(new HtmlBrowserLaunchOptions {
                Proxy = FirstProxy ?? HtmlHttpClientFactory.DefaultProxy,
                ProxyUsername = proxyCredential?.UserName,
                ProxyPassword = proxyCredential?.Password
            });
            policy.OnBlocked = (_, failure, _) => {
                bool render = failure.Kind is HtmlHttpFailureKind.Challenge or HtmlHttpFailureKind.BlockPage;
                WriteVerbose(render ? $"Rendering in a browser after {failure.Provider ?? failure.Kind.ToString()} blocked the request." : failure.ToString());
                return Task.FromResult(render ? HtmlBlockedAction.Render : HtmlBlockedAction.Fail);
            };
        }
        return HttpClientHelper.CreateFetchClient(policy, Proxy, ProxyCredential, UserAgent, Header);
    }

    private static ErrorCategory GetErrorCategory(HtmlHttpFailureKind kind) => kind switch {
        HtmlHttpFailureKind.AuthenticationRequired => ErrorCategory.AuthenticationError,
        HtmlHttpFailureKind.RateLimited => ErrorCategory.LimitsExceeded,
        HtmlHttpFailureKind.HttpError => ErrorCategory.ConnectionError,
        _ => ErrorCategory.PermissionDenied
    };

    private static PSObject ProjectDocument(HtmlPageDocument document) {
        PSObject result = NewObject("PSParseHTML.HtmlPageDocument");
        AddProperty(result, "Title", document.Title);
        AddProperty(result, "Language", document.Language);
        AddProperty(result, "SourceUrl", document.SourceUrl);
        AddProperty(result, "FinalUrl", document.FinalUrl);
        AddProperty(result, "EffectiveBaseUrl", document.EffectiveBaseUrl);
        AddProperty(result, "AnalysisMode", document.AnalysisMode);
        AddProperty(result, "HeadingCount", document.Headings.Count);
        AddProperty(result, "ParagraphCount", document.Paragraphs.Count);
        AddProperty(result, "TableCount", document.Tables.Count);
        AddProperty(result, "CollectionCount", document.Collections.Count);
        AddProperty(result, "Sections", document.Sections);
        AddProperty(result, "Blocks", document.Blocks);
        AddProperty(result, "Headings", document.Headings);
        AddProperty(result, "Paragraphs", document.Paragraphs);
        AddProperty(result, "Lists", document.Lists);
        AddProperty(result, "Tables", document.Tables);
        AddProperty(result, "Links", document.Links);
        AddProperty(result, "Forms", document.Forms);
        AddProperty(result, "Assets", document.Assets);
        AddProperty(result, "Resources", document.Resources);
        AddProperty(result, "Collections", document.Collections.Select(ProjectCollection).ToArray());
        AddProperty(result, "ReadableText", document.ReadableText);
        AddProperty(result, "Markdown", document.Markdown);
        AddProperty(result, "Html", document.Html);
        AddProperty(result, "SemanticDocument", document.SemanticDocument);
        AddProperty(result, "LogicalDocument", document.LogicalDocument);
        AddProperty(result, "Diagnostics", document.Diagnostics);
        AddProperty(result, "Document", document);
        SetDefaultDisplay(
            result,
            "Title",
            "FinalUrl",
            "AnalysisMode",
            "HeadingCount",
            "ParagraphCount",
            "TableCount",
            "CollectionCount");
        return result;
    }

    private static PSObject ProjectCollection(HtmlPageCollection collection) {
        PSObject result = NewObject("PSParseHTML.HtmlPageCollection");
        AddProperty(result, "Index", collection.Index);
        AddProperty(result, "Name", collection.Name);
        AddProperty(result, "Count", collection.Count);
        AddProperty(result, "Confidence", collection.Confidence);
        AddProperty(result, "Score", collection.Score);
        AddProperty(result, "Reason", collection.Reason);
        AddProperty(result, "Fields", collection.Fields);
        AddProperty(result, "Items", collection.Items.Select(ProjectItem).ToArray());
        AddProperty(result, "Selector", collection.Selector);
        AddProperty(result, "Collection", collection);
        SetDefaultDisplay(result, "Index", "Name", "Count", "Confidence");
        return result;
    }

    private static PSObject ProjectItem(HtmlPageCollectionItem item) {
        PSObject result = NewObject("PSParseHTML.HtmlPageCollectionItem");
        AddProperty(result, "Index", item.Index);
        foreach (KeyValuePair<string, object?> value in item.Values) {
            string name = GetProjectedItemPropertyName(value.Key);
            AddProperty(result, name, value.Value);
        }
        AddProperty(result, "Values", item.Values);
        AddProperty(result, "Item", item);
        SetDefaultDisplay(
            result,
            new[] { "Index" }
                .Concat(item.Values.Keys.Select(GetProjectedItemPropertyName))
                .ToArray());
        return result;
    }

    private static PSObject NewObject(string typeName) {
        PSObject result = new();
        result.TypeNames.Insert(0, typeName);
        return result;
    }

    private static void AddProperty(PSObject target, string name, object? value) =>
        target.Properties.Add(new PSNoteProperty(name, value));

    private static void SetDefaultDisplay(PSObject target, params string[] propertyNames) {
        PSPropertySet display = new("DefaultDisplayPropertySet", propertyNames);
        target.Members.Add(new PSMemberSet("PSStandardMembers", new PSMemberInfo[] { display }));
    }

    private static Uri? ParseAbsoluteUri(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ? uri : null;

    private static string GetProjectedItemPropertyName(string name) =>
        name.Equals("Index", StringComparison.OrdinalIgnoreCase)
        || name.Equals("Values", StringComparison.OrdinalIgnoreCase)
        || name.Equals("Item", StringComparison.OrdinalIgnoreCase)
            ? "Field" + name
            : name;
}
