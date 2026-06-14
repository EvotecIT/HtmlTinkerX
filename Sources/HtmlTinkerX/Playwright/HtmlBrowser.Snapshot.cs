using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Helper methods for building rendered page extraction snapshots.
/// </summary>
public static partial class HtmlBrowser {
    /// <summary>
    /// Builds a structured snapshot from an already rendered browser session.
    /// </summary>
    /// <param name="session">Browser session containing a rendered page.</param>
    /// <param name="requestedUrl">Original URL requested by the caller.</param>
    /// <param name="selector">Optional selector used to focus <see cref="HtmlRenderedPageSnapshot.Content"/>.</param>
    /// <param name="innerHtml">Return inner HTML for the focused content.</param>
    /// <param name="asText">Return text for the focused content.</param>
    /// <param name="appliedInteractions">Descriptions of rendered-page interactions applied before extraction.</param>
    /// <param name="staticHtml">Original static HTML used for optional static-vs-rendered comparison.</param>
    /// <param name="includeStaticRenderedComparison">Compares static and rendered HTML when <paramref name="staticHtml"/> is available.</param>
    /// <param name="includeLinkedScripts">Downloads and inspects same-origin linked JavaScript files for endpoints.</param>
    /// <param name="includeExternalLinkedScripts">Allows cross-origin linked JavaScript downloads when linked-script inspection is enabled.</param>
    /// <param name="includeNetworkLog">Include captured browser network entries. This is opt-in because headers may contain sensitive values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A rendered page snapshot with common parsing outputs.</returns>
    public static async Task<HtmlRenderedPageSnapshot> CreateSnapshotAsync(
        HtmlBrowserSession session,
        string requestedUrl,
        string? selector = null,
        bool innerHtml = false,
        bool asText = false,
        IEnumerable<string>? appliedInteractions = null,
        string? staticHtml = null,
        bool includeStaticRenderedComparison = false,
        bool includeLinkedScripts = false,
        bool includeExternalLinkedScripts = false,
        bool includeNetworkLog = false,
        CancellationToken cancellationToken = default) {
        if (session == null) {
            throw new ArgumentNullException(nameof(session));
        }

        string html = await GetContentAsync(session.Page, cancellationToken: cancellationToken).ConfigureAwait(false);
        string text = await GetContentAsync(session.Page, asText: true, cancellationToken: cancellationToken).ConfigureAwait(false);
        string content = string.IsNullOrWhiteSpace(selector) && !asText && !innerHtml
            ? html
            : await GetContentAsync(session.Page, selector, innerHtml, asText, cancellationToken).ConfigureAwait(false);
        string title = await session.Page.TitleAsync().ConfigureAwait(false);
        Uri? baseUri = Uri.TryCreate(session.Page.Url, UriKind.Absolute, out Uri? parsedUri) ? parsedUri : null;
        IReadOnlyList<HtmlDataItem> data = HtmlParsingToolbox.SelectData(html, baseUri: baseUri);
        IReadOnlyList<HtmlJavaScriptConfigItem> javaScriptConfig = HtmlParsingToolbox.SelectJavaScriptConfig(html);
        IReadOnlyList<HtmlInteractionSurfaceItem> interactionSurface = await HtmlParsingToolbox.FindInteractionSurfaceAsync(html, baseUri, includeLinkedScripts, includeExternalLinkedScripts).ConfigureAwait(false);
        IReadOnlyList<HtmlLinkedJavaScriptEndpoint> linkedJavaScriptEndpoints = includeLinkedScripts && baseUri != null
            ? await HtmlLinkedJavaScriptEndpointParser.ParseAsync(html, baseUri, includeExternalLinkedScripts).ConfigureAwait(false)
            : Array.Empty<HtmlLinkedJavaScriptEndpoint>();
        HtmlStaticRenderedComparison? comparison = includeStaticRenderedComparison && staticHtml != null
            ? HtmlParsingToolbox.CompareStaticRendered(staticHtml, html, baseUri)
            : null;
        HtmlReadableTextResult readableText = HtmlParserToText.ExtractReadableText(html, selector);
        string markdown = HtmlParserToMarkdown.ConvertToMarkdown(html, session.Page.Url);

        return new HtmlRenderedPageSnapshot {
            Url = requestedUrl,
            FinalUrl = session.Page.Url,
            Title = title,
            Selector = string.IsNullOrWhiteSpace(selector) ? null : selector,
            ContentKind = GetContentKind(selector, innerHtml, asText),
            Content = content,
            Html = html,
            Text = text,
            ReadableText = readableText,
            Markdown = markdown,
            AppState = HtmlAppStateParser.Parse(html).ToArray(),
            ScriptData = HtmlScriptDataParser.Parse(html).ToArray(),
            Scripts = HtmlWorkflowParser.SelectScripts(html, baseUri).ToArray(),
            JavaScriptEndpoints = HtmlJavaScriptEndpointParser.ParseHtml(html).ToArray(),
            LinkedJavaScriptEndpoints = linkedJavaScriptEndpoints,
            Tokens = HtmlTokenParser.Parse(html).ToArray(),
            Data = data,
            JavaScriptConfig = javaScriptConfig,
            InteractionSurface = interactionSurface,
            StaticRenderedComparison = comparison,
            AppliedInteractions = appliedInteractions?.ToArray() ?? Array.Empty<string>(),
            ConsoleLog = session.ConsoleLog.ToArray(),
            NetworkLog = includeNetworkLog ? session.NetworkLog.ToArray() : Array.Empty<HtmlNetworkEntry>()
        };
    }

    private static string GetContentKind(string? selector, bool innerHtml, bool asText) {
        if (asText) {
            return string.IsNullOrWhiteSpace(selector) ? "DocumentText" : "ElementText";
        }

        if (innerHtml) {
            return "ElementInnerHtml";
        }

        return string.IsNullOrWhiteSpace(selector) ? "DocumentHtml" : "ElementOuterHtml";
    }
}
