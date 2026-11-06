using AngleSharp.Dom;
using HtmlTinkerX;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>Selects elements, text or attribute values from static HTML with a CSS selector or by text.</summary>
/// <remarks>
/// <para>A selector that ends in <c>::text</c> returns each element's own text, and one that ends in <c>::attr(name)</c> returns attribute values.
/// URL attributes such as <c>href</c> and <c>src</c> are resolved against <c>-BaseUrl</c>, the downloaded <c>-Url</c>, or the page's <c>&lt;base href&gt;</c>.
/// Elements without own text or without the attribute are skipped.</para>
/// <para><c>-Text</c> finds elements by their own text, ignoring case unless <c>-CaseSensitive</c> is used. With <c>-Selector</c>, only matching elements are searched.</para>
/// <para><c>-Similar</c> takes the first match and returns the other elements shaped like it, for example the remaining items of a list. A piped element with just <c>-Similar</c> is compared with the rest of its own page.</para>
/// </remarks>
/// <example>
///   <summary>Select product cards from downloaded HTML</summary>
///   <code>ConvertFrom-Html -Content $html | Select-HtmlElement -Selector '.product-card'</code>
/// </example>
/// <example>
///   <summary>Download a page and return its first heading</summary>
///   <code>Select-HtmlElement -Url https://example.org -Selector 'h1' -First</code>
/// </example>
/// <example>
///   <summary>Return absolute link targets and headline text</summary>
///   <code>Select-HtmlElement -Url https://example.org/news -Selector 'article a::attr(href)'
/// Select-HtmlElement -Path .\page.html -Selector 'h2::text'</code>
/// </example>
/// <example>
///   <summary>Find the link labelled Next and return its target</summary>
///   <code>Select-HtmlElement -Content $html -Text 'next' -Selector 'a::attr(href)' -BaseUrl https://example.org/list</code>
/// </example>
/// <example>
///   <summary>Return the titles of the other list items shaped like the featured one</summary>
///   <code>Select-HtmlElement -Path .\shop.html -Selector 'li.featured' -Similar
/// Select-HtmlElement -Path .\shop.html -Text 'Gold One' -Selector 'h2::text' -Similar</code>
/// </example>
/// <example>
///   <summary>Find list items by a regular expression</summary>
///   <code>Select-HtmlElement -Content $html -Text '^v\d+\.\d+' -TextMatch Regex -Selector 'li'</code>
/// </example>
[Cmdlet(VerbsCommon.Select, "HtmlElement", DefaultParameterSetName = ParameterSetInput)]
[Alias("Find-HtmlElement")]
[OutputType(typeof(IElement), typeof(string))]
public sealed class CmdletSelectHtmlElement : AsyncPSCmdlet {
    private const string ParameterSetInput = "Input";
    private const string ParameterSetContent = "Content";
    private const string ParameterSetFile = "File";
    private const string ParameterSetUrl = "Url";

    /// <summary>Parsed document, element, HtmlAgilityPack node, or raw markup to search.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetInput, ValueFromPipeline = true, Position = 0)]
    [Alias("HtmlDocument", "HtmlNode", "Node", "InputObject")]
    public object Input { get; set; } = null!;

    /// <summary>HTML content to search.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent)]
    public string Content { get; set; } = string.Empty;

    /// <summary>Path to an HTML file to search.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFile)]
    [Alias("File")]
    public string Path { get; set; } = string.Empty;

    /// <summary>URL of an HTML page to download and search.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl)]
    [Alias("Uri")]
    public Uri Url { get; set; } = null!;

    /// <summary>CSS selector evaluated against the static document or input element. End it with ::text or ::attr(name) to return strings.</summary>
    [Parameter(Position = 1)]
    public string? Selector { get; set; }

    /// <summary>Find elements whose own text matches this value. Use with -Selector to limit the candidates.</summary>
    [Parameter]
    public string? Text { get; set; }

    /// <summary>How -Text is compared with each element's own text: Exact, Contains or Regex.</summary>
    [Parameter]
    public HtmlTextMatch TextMatch { get; set; } = HtmlTextMatch.Exact;

    /// <summary>Compare -Text case-sensitively.</summary>
    [Parameter]
    public SwitchParameter CaseSensitive { get; set; }

    /// <summary>Return the elements shaped like the first match (same tag and ancestor path, similar classes and children) instead of the match itself.</summary>
    [Parameter]
    public SwitchParameter Similar { get; set; }

    /// <summary>Base URL used to resolve URL attributes selected with ::attr(name). Defaults to Url when downloading.</summary>
    [Parameter]
    public Uri? BaseUrl { get; set; }

    /// <summary>Return only the first matching element.</summary>
    [Parameter]
    [Alias("Single")]
    public SwitchParameter First { get; set; }

    /// <summary>Throw when nothing matches.</summary>
    [Parameter]
    public SwitchParameter Required { get; set; }

    /// <summary>Proxy server address used when downloading by URL.</summary>
    [Parameter(ParameterSetName = ParameterSetUrl)]
    public string? Proxy { get; set; }

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

    private Uri? _effectiveUrl;

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ValidateProxy(Proxy, ProxyCredential);
        // -Similar compares against the rest of the page, so a piped element keeps its own document.
        IElement? pipedElement = Similar.IsPresent && ParameterSetName == ParameterSetInput
            && HtmlPipelineInput.Unwrap(Input) is IElement { Owner: not null } element ? element : null;
        if (string.IsNullOrWhiteSpace(Selector) && string.IsNullOrWhiteSpace(Text) && pipedElement == null) {
            throw new PSArgumentException("Use -Selector, -Text, or both.");
        }

        IParentNode root = pipedElement
            ?? (IParentNode)HtmlParser.ParseWithAngleSharp(await ReadHtmlAsync().ConfigureAwait(false));
        IReadOnlyList<object> results = Select(root);
        if (Required.IsPresent && results.Count == 0) {
            string query = string.IsNullOrWhiteSpace(Text) ? $"CSS selector '{Selector}'" : $"Text '{Text}'";
            ThrowTerminatingError(new ErrorRecord(
                new PSInvalidOperationException($"{query} did not match anything."),
                "HtmlSelectorNoMatch",
                ErrorCategory.ObjectNotFound,
                Selector ?? Text));
            return;
        }

        WriteObject(results, true);
    }

    private IReadOnlyList<object> Select(IParentNode document) {
        HtmlDomExtraction.HtmlValueSelector? valueSelector = string.IsNullOrWhiteSpace(Selector)
            ? null
            : HtmlDomExtraction.ParseValueSelector(Selector!);
        string? elementSelector = valueSelector?.ElementSelector ?? Selector;
        IReadOnlyList<IElement> elements;
        if (!string.IsNullOrWhiteSpace(Text)) {
            elements = HtmlDomExtraction.FindByText(document, Text!, TextMatch, elementSelector, CaseSensitive.IsPresent);
        } else if (valueSelector != null && (!Similar.IsPresent || valueSelector.ElementSelector.Length == 0)) {
            return HtmlDomExtraction.SelectValues(document, Selector!, GetBaseUrl(), First.IsPresent).ToArray<object>();
        } else if (string.IsNullOrEmpty(elementSelector)) {
            elements = Array.Empty<IElement>();
        } else {
            elements = HtmlDomExtraction.QueryElements(document, elementSelector!, nameof(Selector)).ToArray();
        }

        // A piped element is its own anchor when it matches, as it would after reparsing its markup.
        if (document is IElement piped && string.IsNullOrWhiteSpace(Text)
            && (string.IsNullOrEmpty(elementSelector) || piped.Matches(elementSelector!))) {
            elements = new[] { piped };
        }

        if (Similar.IsPresent) {
            IElement? anchor = elements.FirstOrDefault();
            elements = anchor == null ? Array.Empty<IElement>() : HtmlDomExtraction.FindSimilar(anchor);
        }

        if (valueSelector != null) {
            return HtmlDomExtraction.ReadValues(elements, valueSelector, HtmlDomExtraction.ResolveBaseUri(document, GetBaseUrl()), First.IsPresent).ToArray<object>();
        }

        return (First.IsPresent ? elements.Take(1) : elements).ToArray<object>();
    }

    private Uri? GetBaseUrl() => BaseUrl ?? (ParameterSetName == ParameterSetUrl ? _effectiveUrl ?? Url : null);

    private async Task<string> ReadHtmlAsync() {
        switch (ParameterSetName) {
            case ParameterSetContent:
                return Content;
            case ParameterSetFile:
                return await HtmlUtilities.ReadFileCheckedAsync(Path.ToFullPath()).ConfigureAwait(false);
            case ParameterSetUrl:
                using (HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential, UserAgent, Header)) {
                    HtmlHttpTextResult result = await HtmlUtilities.GetTextWithProperEncodingAsync(
                        client,
                        Url.ToString(),
                        fetchOptions: null,
                        cancellationToken: CancelToken).ConfigureAwait(false);
                    _effectiveUrl = result.FinalUri ?? Url;
                    return result.Content;
                }
            default:
                return HtmlPipelineInput.ToHtmlMarkup(Input);
        }
    }
}
