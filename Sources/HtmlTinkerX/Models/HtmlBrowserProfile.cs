namespace HtmlTinkerX;

using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Portable browser profile used to launch consistent browser automation sessions.
/// </summary>
public sealed class HtmlBrowserProfile {
    /// <summary>Friendly profile name.</summary>
    public string? Name { get; set; }

    /// <summary>Browser engine to use when this profile is selected.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public HtmlBrowserEngine? Browser { get; set; }

    /// <summary>Intent-focused scenario defaults applied before explicit profile values.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public HtmlBrowserScenario? Scenario { get; set; }

    /// <summary>Persistent user-data directory for cookies, local storage, permissions, and cache.</summary>
    public string? UserDataDirectory { get; set; }

    /// <summary>Browser distribution channel, such as chrome, msedge, chromium, chrome-beta, or msedge-dev.</summary>
    public string? BrowserChannel { get; set; }

    /// <summary>Path to a browser executable.</summary>
    public string? BrowserExecutablePath { get; set; }

    /// <summary>Chrome DevTools Protocol endpoint URL for attaching to an already-running Chromium browser.</summary>
    public string? CdpEndpointUrl { get; set; }

    /// <summary>Additional browser command-line arguments.</summary>
    public List<string> BrowserArguments { get; set; } = new();

    /// <summary>Enable Chromium sandboxing when supported by the selected browser.</summary>
    public bool? ChromiumSandbox { get; set; }

    /// <summary>User agent string for the browser context.</summary>
    public string? UserAgent { get; set; }

    /// <summary>Initial navigation readiness state.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public HtmlBrowserLoadState? LoadState { get; set; }

    /// <summary>Navigation and selector timeout in milliseconds.</summary>
    public int? Timeout { get; set; }

    /// <summary>Locale used by the browser context.</summary>
    public string? Locale { get; set; }

    /// <summary>Timezone identifier used by the browser context.</summary>
    public string? Timezone { get; set; }

    /// <summary>Viewport width in pixels.</summary>
    public int? ViewportWidth { get; set; }

    /// <summary>Viewport height in pixels.</summary>
    public int? ViewportHeight { get; set; }

    /// <summary>Screen width in pixels.</summary>
    public int? ScreenWidth { get; set; }

    /// <summary>Screen height in pixels.</summary>
    public int? ScreenHeight { get; set; }

    /// <summary>Device scale factor.</summary>
    public float? DeviceScaleFactor { get; set; }

    /// <summary>Whether the context should behave as mobile where supported.</summary>
    public bool? IsMobile { get; set; }

    /// <summary>Whether touch input should be exposed to the page where supported.</summary>
    public bool? HasTouch { get; set; }

    /// <summary>Proxy server URL.</summary>
    public string? Proxy { get; set; }

    /// <summary>Proxy username.</summary>
    public string? ProxyUsername { get; set; }

    /// <summary>Proxy password.</summary>
    public string? ProxyPassword { get; set; }

    /// <summary>Latitude used for geolocation.</summary>
    public double? GeoLatitude { get; set; }

    /// <summary>Longitude used for geolocation.</summary>
    public double? GeoLongitude { get; set; }

    /// <summary>Prevent recognized SSO handoff forms from auto-submitting so they can be inspected.</summary>
    public bool? PreventSsoAutoSubmit { get; set; }

    /// <summary>Browser permissions granted to pages in this context.</summary>
    public List<string> Permissions { get; set; } = new();

    /// <summary>Browser resource types to block before first navigation.</summary>
    public List<HtmlNetworkResourceType> BlockResourceTypes { get; set; } = new();

    /// <summary>Playwright URL glob patterns to block before first navigation.</summary>
    public List<string> BlockResourcePatterns { get; set; } = new();

    /// <summary>JavaScript snippets evaluated before page scripts run.</summary>
    public List<string> InitScripts { get; set; } = new();

    /// <summary>JavaScript files evaluated before page scripts run.</summary>
    public List<string> InitScriptPaths { get; set; } = new();

    /// <summary>Loads a browser profile from a JSON file.</summary>
    public static async Task<HtmlBrowserProfile> LoadAsync(string path, CancellationToken cancellationToken = default) {
        string fullPath = path.ToFullPath();
#if NETSTANDARD2_0 || NETFRAMEWORK
        string json = File.ReadAllText(fullPath);
#else
        string json = await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);
#endif
        return JsonSerializer.Deserialize<HtmlBrowserProfile>(json, CreateJsonOptions())
            ?? throw new InvalidDataException($"Browser profile '{fullPath}' did not contain a valid profile object.");
    }

    /// <summary>Saves this browser profile to a JSON file.</summary>
    public async Task SaveAsync(string path, CancellationToken cancellationToken = default) {
        string fullPath = path.ToFullPath();
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory)) {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(this, CreateJsonOptions());
#if NETSTANDARD2_0 || NETFRAMEWORK
        File.WriteAllText(fullPath, json);
        await Task.CompletedTask.ConfigureAwait(false);
#else
        await File.WriteAllTextAsync(fullPath, json, cancellationToken).ConfigureAwait(false);
#endif
    }

    internal static JsonSerializerOptions CreateJsonOptions() {
        JsonSerializerOptions options = new() {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
