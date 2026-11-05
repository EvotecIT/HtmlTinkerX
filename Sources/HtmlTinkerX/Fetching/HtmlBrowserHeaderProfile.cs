using System;
using System.Collections.Generic;
using System.Globalization;

namespace HtmlTinkerX;

/// <summary>
/// Request headers of one desktop browser version, kept consistent with each other: the User-Agent, Accept,
/// Accept-Language, <c>sec-ch-ua</c> client hints and <c>Sec-Fetch-*</c> navigation headers all describe the same
/// browser. Mixing a browser User-Agent with missing or contradicting client hints is a common bot signal.
/// </summary>
public sealed class HtmlBrowserHeaderProfile {
    /// <summary>The Chrome major version used by <see cref="Chrome"/> when none is given.</summary>
    public const int DefaultChromeMajorVersion = 158;

    private readonly KeyValuePair<string, string>[] _headers;

    private HtmlBrowserHeaderProfile(string userAgent, string acceptLanguage, KeyValuePair<string, string>[] headers) {
        UserAgent = userAgent;
        AcceptLanguage = acceptLanguage;
        _headers = headers;
    }

    /// <summary>The User-Agent header value.</summary>
    public string UserAgent { get; }

    /// <summary>The Accept-Language header value.</summary>
    public string AcceptLanguage { get; }

    /// <summary>Every header the profile sets, in browser order.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Headers => _headers;

    /// <summary>Headers of a top-level page navigation in desktop Chrome on Windows.</summary>
    /// <param name="majorVersion">Chrome major version; pass the current stable version for the most ordinary profile.</param>
    /// <param name="acceptLanguage">The Accept-Language header value.</param>
    public static HtmlBrowserHeaderProfile Chrome(int majorVersion = DefaultChromeMajorVersion, string acceptLanguage = "en-US,en;q=0.9") {
        if (majorVersion < 100 || majorVersion > 999) throw new ArgumentOutOfRangeException(nameof(majorVersion), majorVersion, "Use a Chrome major version between 100 and 999.");
        if (string.IsNullOrWhiteSpace(acceptLanguage)) throw new ArgumentException("Accept-Language is required.", nameof(acceptLanguage));
        string version = majorVersion.ToString(CultureInfo.InvariantCulture);
        string userAgent = $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{version}.0.0.0 Safari/537.36";
        return new HtmlBrowserHeaderProfile(userAgent, acceptLanguage, new[] {
            Header("sec-ch-ua", $"\"Chromium\";v=\"{version}\", \"Google Chrome\";v=\"{version}\", \"Not_A Brand\";v=\"99\""),
            Header("sec-ch-ua-mobile", "?0"),
            Header("sec-ch-ua-platform", "\"Windows\""),
            Header("Upgrade-Insecure-Requests", "1"),
            Header("User-Agent", userAgent),
            Header("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7"),
            Header("Sec-Fetch-Site", "none"),
            Header("Sec-Fetch-Mode", "navigate"),
            Header("Sec-Fetch-User", "?1"),
            Header("Sec-Fetch-Dest", "document"),
            Header("Accept-Language", acceptLanguage)
        });
    }

    /// <summary>Replaces the request's values for every header in the profile.</summary>
    internal void Apply(HttpRequestMessage request) {
        foreach (KeyValuePair<string, string> header in _headers) {
            request.Headers.Remove(header.Key);
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
    }

    private static KeyValuePair<string, string> Header(string name, string value) => new(name, value);
}
