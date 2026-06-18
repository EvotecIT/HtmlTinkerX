using HtmlTinkerX;
using System.Collections;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Maps common PowerShell browser-launch parameters to reusable HtmlTinkerX launch options.
/// </summary>
internal static class HtmlBrowserLaunchOptionFactory {
    /// <summary>
    /// Creates launch options from common cmdlet parameters while preserving profile, scenario, and explicit-parameter precedence.
    /// </summary>
    /// <param name="request">PowerShell launch parameter values.</param>
    /// <param name="cancellationToken">Token used while loading optional profile JSON.</param>
    public static async Task<HtmlBrowserLaunchOptions> CreateAsync(HtmlBrowserLaunchOptionRequest request, CancellationToken cancellationToken) {
        if (request.BlockResourceType != null && request.BlockResourceType.Contains(HtmlNetworkResourceType.Document)) {
            throw new PSArgumentException("BlockResourceType Document would abort page navigation. Block subresources such as Image, Media, Font, Stylesheet, Script, XHR, or Fetch instead.");
        }

        HtmlBrowserLaunchOptions options = request.BaseOptions ?? new HtmlBrowserLaunchOptions();
        if (!string.IsNullOrWhiteSpace(request.ProfilePath)) {
            HtmlBrowserProfile profile = await HtmlBrowserProfile.LoadAsync(request.ProfilePath!, cancellationToken).ConfigureAwait(false);
            options.ApplyProfile(profile);
        }

        if (IsBound(request.BoundParameters, nameof(request.Scenario))) {
            options.ApplyScenario(request.Scenario);
        }

        options.Browser = IsBound(request.BoundParameters, nameof(request.Browser)) ? request.Browser : options.Browser;
        if (IsBound(request.BoundParameters, nameof(request.Clean))) {
            options.Clean = request.Clean.IsPresent;
        }

        if (IsBound(request.BoundParameters, nameof(request.Visible))) {
            options.Headless = false;
        }

        if (IsBound(request.BoundParameters, nameof(request.SlowMo))) {
            options.SlowMo = request.SlowMo;
        }
        options.Proxy = request.Proxy ?? options.Proxy;
        options.ProxyUsername = request.ProxyCredential?.UserName ?? options.ProxyUsername;
        options.ProxyPassword = request.ProxyCredential?.GetNetworkCredential().Password ?? options.ProxyPassword;
        options.LoadState = IsBound(request.BoundParameters, nameof(request.LoadState)) ? request.LoadState : options.LoadState;
        options.Timeout = IsBound(request.BoundParameters, request.TimeoutParameterName) ? request.Timeout : options.Timeout;

        SetIfBound(request, nameof(request.UserDataDirectory), value => options.UserDataDirectory = value, request.UserDataDirectory?.ToFullPath());
        SetIfBound(request, nameof(request.StatePath), value => options.StorageStatePath = value, request.StatePath?.ToFullPath());
        SetIfBound(request, nameof(request.BrowserChannel), value => options.BrowserChannel = value, request.BrowserChannel);
        SetIfBound(request, nameof(request.BrowserExecutablePath), value => options.BrowserExecutablePath = value, request.BrowserExecutablePath?.ToFullPath());
        SetIfBound(request, nameof(request.CdpEndpointUrl), value => options.CdpEndpointUrl = value, request.CdpEndpointUrl);
        SetIfBound(request, nameof(request.UserAgent), value => options.UserAgent = value, request.UserAgent);
        SetIfBound(request, nameof(request.Locale), value => options.Locale = value, request.Locale);
        SetIfBound(request, nameof(request.ViewportWidth), value => options.ViewportWidth = value, request.ViewportWidth);
        SetIfBound(request, nameof(request.ViewportHeight), value => options.ViewportHeight = value, request.ViewportHeight);
        SetIfBound(request, nameof(request.ScreenWidth), value => options.ScreenWidth = value, request.ScreenWidth);
        SetIfBound(request, nameof(request.ScreenHeight), value => options.ScreenHeight = value, request.ScreenHeight);
        SetIfBound(request, nameof(request.DeviceScaleFactor), value => options.DeviceScaleFactor = (float?)value, request.DeviceScaleFactor);
        SetIfBound(request, nameof(request.GeoLatitude), value => options.GeoLatitude = value, request.GeoLatitude);
        SetIfBound(request, nameof(request.GeoLongitude), value => options.GeoLongitude = value, request.GeoLongitude);
        SetIfBound(request, nameof(request.Timezone), value => options.Timezone = value, request.Timezone);

        if (IsBound(request.BoundParameters, nameof(request.ChromiumSandbox)) && request.ChromiumSandbox.IsPresent) {
            options.ChromiumSandbox = true;
        }

        if (IsBound(request.BoundParameters, nameof(request.Mobile)) && request.Mobile.IsPresent) {
            options.IsMobile = true;
        }

        if (IsBound(request.BoundParameters, nameof(request.Touch)) && request.Touch.IsPresent) {
            options.HasTouch = true;
        }

        AddRange(options.BrowserArguments, request.BrowserArgument);
        AddRange(options.Permissions, request.Permission);
        AddRange(options.InitScripts, request.InitScript);
        AddRange(options.InitScriptPaths, request.InitScriptPath);
        AddRange(options.BlockResourceTypes, request.BlockResourceType);
        AddRange(options.BlockResourcePatterns, request.BlockResourcePattern);
        return options;
    }

    private static void SetIfBound<T>(HtmlBrowserLaunchOptionRequest request, string parameterName, System.Action<T?> setter, T? value) {
        if (IsBound(request.BoundParameters, parameterName)) {
            setter(value);
        }
    }

    private static bool IsBound(IDictionary parameters, string parameterName)
        => parameters.Contains(parameterName);

    private static void AddRange<T>(System.Collections.Generic.ICollection<T> target, System.Collections.Generic.IEnumerable<T>? values) {
        if (values == null) {
            return;
        }

        foreach (T value in values) {
            target.Add(value);
        }
    }
}
