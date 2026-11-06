using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HtmlTinkerX;

/// <summary>
/// Request headers of one desktop browser version, kept consistent with each other: the User-Agent, Accept,
/// Accept-Language, <c>sec-ch-ua</c> client hints and <c>Sec-Fetch-*</c> navigation headers all describe the same
/// browser. Mixing a browser User-Agent with missing or contradicting client hints is a common bot signal.
/// </summary>
/// <remarks>
/// <see cref="Chrome"/> describes an ordinary desktop Chrome without any browser being involved.
/// <see cref="HtmlBrowser.GetHeaderProfileAsync"/> reads the profile from the browser a session would really use, so
/// HTTP requests and a browser fallback given the same profile through
/// <see cref="HtmlBrowserLaunchOptions.HeaderProfile"/> present one identity.
/// </remarks>
public sealed class HtmlBrowserHeaderProfile {
    /// <summary>The Chrome major version used by <see cref="Chrome"/> when none is given.</summary>
    public const int DefaultChromeMajorVersion = 158;

    private const string ChromeAccept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7";
    private const string DocumentAccept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";

    private readonly KeyValuePair<string, string>[] _headers;

    private HtmlBrowserHeaderProfile(string userAgent, string acceptLanguage, KeyValuePair<string, string>[] headers,
        IReadOnlyList<KeyValuePair<string, string>>? brands, bool mobile, string? platform) {
        UserAgent = userAgent;
        AcceptLanguage = acceptLanguage;
        Locale = PrimaryLanguage(acceptLanguage);
        _headers = headers;
        Brands = brands;
        Mobile = mobile;
        Platform = platform;
    }

    /// <summary>The client hint brands and their versions, or null for a browser that sends no client hints.</summary>
    internal IReadOnlyList<KeyValuePair<string, string>>? Brands { get; }

    internal bool Mobile { get; }

    internal string? Platform { get; }

    /// <summary>The User-Agent header value.</summary>
    public string UserAgent { get; }

    /// <summary>The Accept-Language header value.</summary>
    public string AcceptLanguage { get; }

    /// <summary>The first language of <see cref="AcceptLanguage"/>, which a browser given this profile uses as its locale.</summary>
    public string Locale { get; }

    /// <summary>Every header the profile sets, in browser order.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Headers => _headers;

    /// <summary>Headers of a top-level page navigation in desktop Chrome on Windows.</summary>
    /// <param name="majorVersion">Chrome major version; pass the current stable version for the most ordinary profile.</param>
    /// <param name="acceptLanguage">The Accept-Language header value.</param>
    public static HtmlBrowserHeaderProfile Chrome(int majorVersion = DefaultChromeMajorVersion, string acceptLanguage = "en-US,en;q=0.9") {
        if (majorVersion < 100 || majorVersion > 999) throw new ArgumentOutOfRangeException(nameof(majorVersion), majorVersion, "Use a Chrome major version between 100 and 999.");
        string version = majorVersion.ToString(CultureInfo.InvariantCulture);
        string userAgent = $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{version}.0.0.0 Safari/537.36";
        KeyValuePair<string, string>[] brands = { Header("Chromium", version), Header("Google Chrome", version), Header("Not_A Brand", "99") };
        return Create(userAgent, brands, mobile: false, "Windows", ChromeAccept, acceptLanguage);
    }

    /// <summary>
    /// A profile of a browser as it reported itself. Headless Chromium names itself HeadlessChrome; that word is
    /// replaced with the brand the same build reports when it runs headed, so the version and platform stay the real ones.
    /// </summary>
    internal static HtmlBrowserHeaderProfile FromBrowser(string userAgent, IReadOnlyList<KeyValuePair<string, string>>? brands, bool mobile, string? platform, string acceptLanguage) {
        if (string.IsNullOrWhiteSpace(userAgent)) throw new ArgumentException("The browser reported no User-Agent.", nameof(userAgent));
        string headedAgent = userAgent.Replace("HeadlessChrome/", "Chrome/");
        KeyValuePair<string, string>[]? headedBrands = brands is { Count: > 0 }
            ? brands.Select(static brand => Header(brand.Key == "HeadlessChrome" ? "Chromium" : brand.Key, brand.Value)).ToArray()
            : null;
        return Create(headedAgent, headedBrands, mobile, platform, headedBrands != null ? ChromeAccept : DocumentAccept, acceptLanguage);
    }

    /// <summary>Replaces the request's values for every header in the profile.</summary>
    internal void Apply(HttpRequestMessage request) {
        foreach (KeyValuePair<string, string> header in _headers) {
            request.Headers.Remove(header.Key);
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
    }

    private static HtmlBrowserHeaderProfile Create(string userAgent, KeyValuePair<string, string>[]? brands, bool mobile, string? platform, string accept, string acceptLanguage) {
        if (string.IsNullOrWhiteSpace(acceptLanguage)) throw new ArgumentException("Accept-Language is required.", nameof(acceptLanguage));
        List<KeyValuePair<string, string>> headers = new();
        if (brands != null) {
            headers.Add(Header("sec-ch-ua", string.Join(", ", brands.Select(static brand => $"\"{brand.Key}\";v=\"{brand.Value}\""))));
            headers.Add(Header("sec-ch-ua-mobile", mobile ? "?1" : "?0"));
            if (!string.IsNullOrWhiteSpace(platform)) headers.Add(Header("sec-ch-ua-platform", $"\"{platform}\""));
        }
        headers.Add(Header("Upgrade-Insecure-Requests", "1"));
        headers.Add(Header("User-Agent", userAgent));
        headers.Add(Header("Accept", accept));
        headers.Add(Header("Sec-Fetch-Site", "none"));
        headers.Add(Header("Sec-Fetch-Mode", "navigate"));
        headers.Add(Header("Sec-Fetch-User", "?1"));
        headers.Add(Header("Sec-Fetch-Dest", "document"));
        headers.Add(Header("Accept-Language", acceptLanguage));
        return new HtmlBrowserHeaderProfile(userAgent, acceptLanguage, headers.ToArray(), brands, mobile, platform);
    }

    private static string PrimaryLanguage(string acceptLanguage) {
        string first = acceptLanguage.Split(',')[0];
        int quality = first.IndexOf(';');
        return (quality >= 0 ? first.Substring(0, quality) : first).Trim();
    }

    private static KeyValuePair<string, string> Header(string name, string value) => new(name, value);
}
