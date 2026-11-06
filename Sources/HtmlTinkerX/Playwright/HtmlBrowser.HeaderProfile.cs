using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

public static partial class HtmlBrowser {
    private const string IdentityProbe = "https://identity.htmltinkerx.invalid/";

    private const string IdentityScript = @"() => JSON.stringify({
        userAgent: navigator.userAgent,
        languages: Array.from(navigator.languages || []),
        brands: navigator.userAgentData ? navigator.userAgentData.brands.map(b => [b.brand, b.version]) : null,
        mobile: navigator.userAgentData ? navigator.userAgentData.mobile : false,
        platform: navigator.userAgentData ? navigator.userAgentData.platform : null
    })";

    /// <summary>
    /// Reads the request headers of the browser the options launch or attach to, for HTTP requests that should
    /// present the same identity as a browser fallback. Opens and closes one session.
    /// </summary>
    /// <remarks>
    /// The User-Agent, version and platform are the browser's own. Headless Chromium calls itself HeadlessChrome; the
    /// profile names it as the same build does when headed, and a session given the profile through
    /// <see cref="HtmlBrowserLaunchOptions.HeaderProfile"/> reports that name too. Firefox and WebKit send no client hints.
    /// </remarks>
    /// <param name="options">Browser launch options; headless Chromium when null. Must not set a HeaderProfile.</param>
    /// <param name="acceptLanguage">Languages to send, or null for the browser's own; weighted as Chrome weighs them, which a Chromium session given the profile sends too.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task<HtmlBrowserHeaderProfile> GetHeaderProfileAsync(HtmlBrowserLaunchOptions? options = null, string? acceptLanguage = null, CancellationToken cancellationToken = default) {
        HtmlBrowserLaunchOptions launch = options ?? new HtmlBrowserLaunchOptions();
        if (launch.HeaderProfile != null) throw new ArgumentException("The profile is read from the browser itself; clear HeaderProfile first.", nameof(options));
        await using HtmlBrowserSession session = await OpenSessionForNavigationAsync(launch, cancellationToken).ConfigureAwait(false);
        // Client hints exist only in a secure context; a page answered here, never by the network, provides one.
        await session.Page.RouteAsync(IdentityProbe, static route => route.FulfillAsync(new RouteFulfillOptions {
            Status = 200, ContentType = "text/html", Body = "<!doctype html><title>identity</title>"
        })).ConfigureAwait(false);
        await session.Page.GotoAsync(IdentityProbe).WaitWithCancellationAsync(cancellationToken).ConfigureAwait(false);
        string json = await session.Page.EvaluateAsync<string>(IdentityScript).ConfigureAwait(false);
        using JsonDocument identity = JsonDocument.Parse(json);
        JsonElement root = identity.RootElement;
        List<KeyValuePair<string, string>>? brands = root.GetProperty("brands").ValueKind == JsonValueKind.Array
            ? root.GetProperty("brands").EnumerateArray().Select(static brand => new KeyValuePair<string, string>(brand[0].GetString()!, brand[1].GetString()!)).ToList()
            : null;
        string[] languages = root.GetProperty("languages").EnumerateArray().Select(static language => language.GetString()!).ToArray();
        return HtmlBrowserHeaderProfile.FromBrowser(
            root.GetProperty("userAgent").GetString()!,
            brands,
            root.GetProperty("mobile").GetBoolean(),
            root.GetProperty("platform").ValueKind == JsonValueKind.String ? root.GetProperty("platform").GetString() : null,
            acceptLanguage != null ? WeighLanguages(LanguageTags(acceptLanguage)) : ToAcceptLanguage(languages));
    }

    /// <summary>Formats navigator.languages the way Chrome does: each region tag followed by its language, with falling weights.</summary>
    internal static string ToAcceptLanguage(IEnumerable<string> languages) {
        string[] reported = languages.Where(static language => !string.IsNullOrWhiteSpace(language)).ToArray();
        List<string> tags = new();
        foreach (string language in reported) {
            if (!tags.Contains(language, StringComparer.OrdinalIgnoreCase)) tags.Add(language);
            int region = language.IndexOf('-');
            string baseLanguage = region > 0 ? language.Substring(0, region) : language;
            if (!tags.Contains(baseLanguage, StringComparer.OrdinalIgnoreCase) && !reported.Contains(baseLanguage, StringComparer.OrdinalIgnoreCase)) tags.Add(baseLanguage);
        }
        return tags.Count == 0 ? "en-US,en;q=0.9" : WeighLanguages(tags);
    }

    /// <summary>The language tags of an Accept-Language value, without their weights.</summary>
    internal static string[] LanguageTags(string acceptLanguage) =>
        acceptLanguage.Split(',').Select(static part => part.Split(';')[0].Trim()).Where(static tag => tag.Length > 0).ToArray();

    /// <summary>Chrome's weights: the first tag unweighted, then 0.9, 0.8 and so on down to 0.1.</summary>
    private static string WeighLanguages(IReadOnlyList<string> tags) {
        if (tags.Count == 0) throw new ArgumentException("Accept-Language needs at least one language.");
        return string.Join(",", tags.Select((tag, index) => index == 0
            ? tag
            : tag + ";q=" + Math.Max(0.1, 1 - index / 10.0).ToString("0.0", CultureInfo.InvariantCulture)));
    }
}
