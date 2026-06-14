using HtmlTinkerX;
using System;
using System.IO;
using System.Management.Automation;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that retrieves HTML content after executing JavaScript using a headless browser.
/// </summary>
/// <example>
/// <code>Invoke-HTMLRendering -Url https://example.com -Browser Chromium -Clean</code>
/// </example>
[Cmdlet(VerbsLifecycle.Invoke, "HTMLRendering", DefaultParameterSetName = ParameterSetDefault)]
[Alias("Start-HTMLSession", "Open-HTMLSession")]
[OutputType(typeof(string), typeof(HtmlBrowserSession), typeof(HtmlRenderedPageSnapshot))]
public sealed class CmdletInvokeHtmlRendering : AsyncPSCmdlet {
    private const string ParameterSetDefault = "Default";
    private const string ParameterSetFile = "File";

    /// <summary>URL of the web page.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetDefault)]
    public string Url { get; set; } = string.Empty;

    /// <summary>Path to a local HTML file.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetFile)]
    [Alias("File")]
    public string? Path { get; set; }

    /// <summary>Optional file path to save the rendered HTML.</summary>
    [Parameter]
    public string? OutFile { get; set; }

    /// <summary>Browser engine to use for rendering.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public HtmlBrowserEngine Browser { get; set; } = HtmlBrowserEngine.Chromium;

    /// <summary>Force re-download of browser runtimes.</summary>
    [Parameter(ParameterSetName = ParameterSetDefault)]
    [Parameter(ParameterSetName = ParameterSetFile)]
    public SwitchParameter Clean { get; set; }

    /// <summary>Proxy server address used when launching the browser.</summary>
    [Parameter]
    public string? Proxy { get; set; }

    /// <summary>Credentials used for the <see cref="Proxy"/> server.</summary>
    [Parameter]
    public PSCredential? ProxyCredential { get; set; }

    /// <summary>Credentials used when accessing authenticated pages.</summary>
    [Parameter]
    public PSCredential? Credential { get; set; }

    /// <summary>Username for pages secured with basic authentication.</summary>
    [Parameter]
    public string? Username { get; set; }

    /// <summary>Password for pages secured with basic authentication.</summary>
    [Parameter]
    public string? Password { get; set; }

    /// <summary>URL for login form when using form authentication.</summary>
    [Parameter]
    public string? LoginUrl { get; set; }

    /// <summary>CSS selector for the username field of the login form.</summary>
    [Parameter]
    public string? UsernameSelector { get; set; }

    /// <summary>CSS selector for the password field of the login form.</summary>
    [Parameter]
    public string? PasswordSelector { get; set; }

    /// <summary>CSS selector for the submit element of the login form.</summary>
    [Parameter]
    public string? SubmitSelector { get; set; }

    /// <summary>Return a browser session instead of HTML.</summary>
    [Parameter]
    public SwitchParameter Session { get; set; }

    /// <summary>Optional Playwright storage state file used to reuse cookies, local storage, and authenticated browser state.</summary>
    [Parameter]
    public string? StorageStatePath { get; set; }

    /// <summary>Optional CSS selector used to return one rendered element instead of the full document.</summary>
    [Parameter]
    public string? Selector { get; set; }

    /// <summary>Return inner HTML for the selected element instead of outer HTML.</summary>
    [Parameter]
    public SwitchParameter InnerHtml { get; set; }

    /// <summary>Return rendered text instead of HTML markup.</summary>
    [Parameter]
    public SwitchParameter AsText { get; set; }

    /// <summary>Return a structured rendered-page snapshot with common parsed app data instead of raw content.</summary>
    [Parameter]
    public SwitchParameter Snapshot { get; set; }

    /// <summary>Include browser network entries in Snapshot output. Headers may contain sensitive values.</summary>
    [Parameter]
    public SwitchParameter IncludeNetworkLog { get; set; }

    /// <summary>Preset rendering strategy for common dynamic-page scenarios.</summary>
    [Parameter]
    public HtmlRenderProfile RenderProfile { get; set; } = HtmlRenderProfile.Custom;

    /// <summary>Include a static-vs-rendered comparison in Snapshot output.</summary>
    [Parameter]
    public SwitchParameter IncludeStaticRenderedComparison { get; set; }

    /// <summary>Download and inspect same-origin linked JavaScript files for endpoint discovery in Snapshot output.</summary>
    [Parameter]
    public SwitchParameter IncludeLinkedScripts { get; set; }

    /// <summary>Allow cross-origin linked JavaScript downloads when IncludeLinkedScripts is used.</summary>
    [Parameter]
    public SwitchParameter IncludeExternalLinkedScripts { get; set; }

    /// <summary>Capture response bodies for selected network requests in Snapshot output. Bodies may contain sensitive values.</summary>
    [Parameter]
    public SwitchParameter IncludeResponseBody { get; set; }

    /// <summary>Maximum UTF-8 bytes stored per captured response body.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int ResponseBodyMaxBytes { get; set; } = 65536;

    /// <summary>Network resource types whose response bodies should be captured. Defaults to XHR and Fetch.</summary>
    [Parameter]
    public HtmlNetworkResourceType[] ResponseBodyResourceType { get; set; } = System.Array.Empty<HtmlNetworkResourceType>();

    /// <summary>Optional CSS selector to wait for before extracting rendered content.</summary>
    [Parameter]
    public string? WaitForSelector { get; set; }

    /// <summary>Optional JavaScript predicate to wait for before extracting rendered content.</summary>
    [Parameter]
    public string? WaitForFunction { get; set; }

    /// <summary>Initial browser navigation readiness state.</summary>
    [Parameter]
    public HtmlBrowserLoadState LoadState { get; set; } = HtmlBrowserLoadState.NetworkIdle;

    /// <summary>Browser resource types to abort before navigation, such as Image, Media, Font, or Stylesheet.</summary>
    [Parameter]
    public HtmlNetworkResourceType[] BlockResourceType { get; set; } = System.Array.Empty<HtmlNetworkResourceType>();

    /// <summary>Playwright URL glob patterns to abort before navigation, such as **/analytics/**.</summary>
    [Parameter]
    public string[] BlockResourcePattern { get; set; } = System.Array.Empty<string>();

    /// <summary>Optional selectors to click before extraction.</summary>
    [Parameter]
    public string[] ClickSelector { get; set; } = System.Array.Empty<string>();

    /// <summary>Optional visible texts to click before extraction.</summary>
    [Parameter]
    public string[] ClickText { get; set; } = System.Array.Empty<string>();

    /// <summary>Optional selectors to dismiss before normal click interactions.</summary>
    [Parameter]
    public string[] DismissSelector { get; set; } = System.Array.Empty<string>();

    /// <summary>Optional visible texts to dismiss before normal click interactions.</summary>
    [Parameter]
    public string[] DismissText { get; set; } = System.Array.Empty<string>();

    /// <summary>Delay after each rendered interaction in milliseconds.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int InteractionDelayMs { get; set; } = 300;

    /// <summary>Number of times click interactions should be retried on rendered pages.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int InteractionRepeatCount { get; set; } = 1;

    /// <summary>Optional delay after rendered page load in milliseconds.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int WaitAfterLoadMs { get; set; }

    /// <summary>Scroll the rendered page before extraction to trigger lazy-loaded content.</summary>
    [Parameter]
    public SwitchParameter AutoScroll { get; set; }

    /// <summary>Number of incremental scroll steps performed when AutoScroll is enabled.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int AutoScrollSteps { get; set; } = 3;

    /// <summary>Delay after each auto-scroll step in milliseconds.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int AutoScrollDelayMs { get; set; } = 400;

    /// <summary>Do not set the opened session as the default session.</summary>
    [Parameter]
    public SwitchParameter NoDefault { get; set; }

    /// <summary>Show the browser instead of running headless.</summary>
    [Parameter]
    public SwitchParameter Visible { get; set; }

    /// <summary>Slow down Playwright actions by the specified milliseconds.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int SlowMo { get; set; } = 0;

    /// <summary>Timeout in milliseconds for browser operations.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int Timeout { get; set; } = 10000;

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <summary>User agent string used when launching the browser.</summary>
    [Parameter]
    public string? UserAgent { get; set; }

    /// <summary>Viewport width in pixels.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int? ViewportWidth { get; set; }

    /// <summary>Viewport height in pixels.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int? ViewportHeight { get; set; }

    /// <summary>Scaling factor for high DPI devices.</summary>
    [Parameter]
    public double? DeviceScaleFactor { get; set; }

    /// <summary>Latitude used for geolocation.</summary>
    [Parameter]
    public double? GeoLatitude { get; set; }

    /// <summary>Longitude used for geolocation.</summary>
    [Parameter]
    public double? GeoLongitude { get; set; }

    /// <summary>Timezone identifier.</summary>
    [Parameter]
    public string? Timezone { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        ApplyRenderProfile();

        if (AsText.IsPresent && InnerHtml.IsPresent) {
            throw new PSArgumentException("Use either -AsText or -InnerHtml, not both.");
        }
        if (InnerHtml.IsPresent && string.IsNullOrWhiteSpace(Selector)) {
            throw new PSArgumentException("InnerHtml requires Selector.");
        }
        if (Snapshot.IsPresent && Session.IsPresent) {
            throw new PSArgumentException("Use either -Snapshot or -Session, not both.");
        }
        if (Session.IsPresent && (!string.IsNullOrWhiteSpace(Selector) || InnerHtml.IsPresent || AsText.IsPresent)) {
            throw new PSArgumentException("Selector, InnerHtml, and AsText are content extraction options and cannot be used with -Session.");
        }
        if (Snapshot.IsPresent && !string.IsNullOrEmpty(OutFile)) {
            throw new PSArgumentException("Snapshot output is an object and cannot be written with -OutFile. Omit -Snapshot when saving rendered content.");
        }
        if (IncludeNetworkLog.IsPresent && !Snapshot.IsPresent) {
            throw new PSArgumentException("IncludeNetworkLog is only valid with -Snapshot.");
        }
        if (IncludeStaticRenderedComparison.IsPresent && !Snapshot.IsPresent) {
            throw new PSArgumentException("IncludeStaticRenderedComparison is only valid with -Snapshot.");
        }
        if (IncludeLinkedScripts.IsPresent && !Snapshot.IsPresent) {
            throw new PSArgumentException("IncludeLinkedScripts is only valid with -Snapshot.");
        }
        if (IncludeExternalLinkedScripts.IsPresent && !IncludeLinkedScripts.IsPresent) {
            throw new PSArgumentException("IncludeExternalLinkedScripts requires -IncludeLinkedScripts.");
        }
        if (IncludeResponseBody.IsPresent && !Snapshot.IsPresent) {
            throw new PSArgumentException("IncludeResponseBody is only valid with -Snapshot.");
        }
        if (string.IsNullOrWhiteSpace(WaitForSelector)
            && string.IsNullOrWhiteSpace(WaitForFunction)
            && LoadState == HtmlBrowserLoadState.Commit
            && WaitAfterLoadMs == 0
            && RenderProfile != HtmlRenderProfile.HeavyDynamicPage) {
            throw new PSArgumentException("LoadState Commit requires WaitForSelector, WaitForFunction, or WaitAfterLoadMs so content extraction has a readiness signal.");
        }
        if (System.Array.IndexOf(BlockResourceType, HtmlNetworkResourceType.Document) >= 0) {
            throw new PSArgumentException("BlockResourceType Document would abort page navigation. Block subresources such as Image, Media, Font, Stylesheet, Script, XHR, or Fetch instead.");
        }

        ValidateProxy(Proxy, ProxyCredential);
        string? user = Credential?.UserName ?? Username;
        string? pass = Credential?.GetNetworkCredential().Password ?? Password;
        string? pUser = ProxyCredential?.UserName;
        string? pPass = ProxyCredential?.GetNetworkCredential().Password;
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        CancellationToken token = linkedCts.Token;
        HtmlFormLogin? form = null;
        if (!string.IsNullOrEmpty(LoginUrl) && !string.IsNullOrEmpty(UsernameSelector) && !string.IsNullOrEmpty(PasswordSelector) && !string.IsNullOrEmpty(SubmitSelector)) {
            form = new HtmlFormLogin {
                LoginUrl = LoginUrl!,
                UsernameSelector = UsernameSelector!,
                PasswordSelector = PasswordSelector!,
                SubmitSelector = SubmitSelector!
            };
        }

        string target = ParameterSetName == ParameterSetFile
            ? new System.Uri(Path!.ToFullPath()).AbsoluteUri
            : Url;
        string? staticHtml = IncludeStaticRenderedComparison.IsPresent
            ? await ReadStaticHtmlAsync(target, token).ConfigureAwait(false)
            : null;

        if (Session.IsPresent) {
            HtmlBrowserSession sess = await HtmlBrowser.OpenSessionWithOptionsAsync(
                target,
                Browser,
                Clean.IsPresent,
                user,
                pass,
                form,
                headless: !Visible.IsPresent,
                slowMo: SlowMo,
                storageStatePath: StorageStatePath?.ToFullPath(),
                userAgent: UserAgent,
                viewportWidth: ViewportWidth,
                viewportHeight: ViewportHeight,
                deviceScaleFactor: (float?)DeviceScaleFactor,
                proxy: Proxy,
                proxyUsername: pUser,
                proxyPassword: pPass,
                geoLatitude: GeoLatitude,
                geoLongitude: GeoLongitude,
                timezone: Timezone,
                blockResourceTypes: BlockResourceType,
                blockResourcePatterns: BlockResourcePattern,
                loadState: LoadState,
                timeout: Timeout,
                cancellationToken: token).ConfigureAwait(false);
            try {
                await HtmlBrowser.PreparePageForContentAsync(
                    sess.Page,
                    waitForSelector: WaitForSelector,
                    waitForFunction: WaitForFunction,
                    clickSelectors: ClickSelector,
                    clickTexts: ClickText,
                    dismissSelectors: DismissSelector,
                    dismissTexts: DismissText,
                    interactionDelayMs: InteractionDelayMs,
                    interactionRepeatCount: InteractionRepeatCount,
                    waitAfterLoadMs: WaitAfterLoadMs,
                    autoScroll: AutoScroll.IsPresent,
                    autoScrollSteps: AutoScrollSteps,
                    autoScrollDelayMs: AutoScrollDelayMs,
                    timeout: Timeout,
                    cancellationToken: token).ConfigureAwait(false);
            } catch {
                await sess.DisposeAsync().ConfigureAwait(false);
                throw;
            }

            if (!NoDefault.IsPresent) {
                SessionState.PSVariable.Set("PSParseHTML_DefaultSession", sess);
            }
            WriteObject(sess);
        } else {
            await using HtmlBrowserSession sess = await HtmlBrowser.OpenSessionWithOptionsAsync(
                target,
                Browser,
                Clean.IsPresent,
                user,
                pass,
                form,
                headless: !Visible.IsPresent,
                slowMo: SlowMo,
                storageStatePath: StorageStatePath?.ToFullPath(),
                userAgent: UserAgent,
                viewportWidth: ViewportWidth,
                viewportHeight: ViewportHeight,
                deviceScaleFactor: (float?)DeviceScaleFactor,
                proxy: Proxy,
                proxyUsername: pUser,
                proxyPassword: pPass,
                geoLatitude: GeoLatitude,
                geoLongitude: GeoLongitude,
                timezone: Timezone,
                blockResourceTypes: BlockResourceType,
                blockResourcePatterns: BlockResourcePattern,
                loadState: LoadState,
                timeout: Timeout,
                cancellationToken: token).ConfigureAwait(false);
            var appliedInteractions = await HtmlBrowser.PreparePageForContentAsync(
                sess.Page,
                waitForSelector: WaitForSelector,
                waitForFunction: WaitForFunction,
                clickSelectors: ClickSelector,
                clickTexts: ClickText,
                dismissSelectors: DismissSelector,
                dismissTexts: DismissText,
                interactionDelayMs: InteractionDelayMs,
                interactionRepeatCount: InteractionRepeatCount,
                waitAfterLoadMs: WaitAfterLoadMs,
                autoScroll: AutoScroll.IsPresent,
                autoScrollSteps: AutoScrollSteps,
                autoScrollDelayMs: AutoScrollDelayMs,
                timeout: Timeout,
                cancellationToken: token).ConfigureAwait(false);
            if (IncludeResponseBody.IsPresent) {
                HtmlNetworkResourceType[]? responseBodyResourceTypes = MyInvocation.BoundParameters.ContainsKey(nameof(ResponseBodyResourceType))
                    ? ResponseBodyResourceType
                    : null;
                await HtmlBrowser.CaptureResponseBodiesAsync(sess, ResponseBodyMaxBytes, responseBodyResourceTypes, token).ConfigureAwait(false);
            }
            if (Snapshot.IsPresent) {
                HttpClient? snapshotHttpClient = IncludeLinkedScripts.IsPresent
                    ? HttpClientHelper.Create(Proxy, ProxyCredential, Credential, Username, Password)
                    : null;
                try {
                    HtmlRenderedPageSnapshot snapshot = await HtmlBrowser.CreateSnapshotAsync(
                        sess,
                        target,
                        Selector,
                        InnerHtml.IsPresent,
                        AsText.IsPresent,
                        appliedInteractions,
                        staticHtml,
                        IncludeStaticRenderedComparison.IsPresent,
                        IncludeLinkedScripts.IsPresent,
                        IncludeExternalLinkedScripts.IsPresent,
                        IncludeNetworkLog.IsPresent || IncludeResponseBody.IsPresent,
                        token,
                        snapshotHttpClient,
                        Timeout).ConfigureAwait(false);
                    WriteObject(snapshot);
                } finally {
                    snapshotHttpClient?.Dispose();
                }
                return;
            }

            string html = await HtmlBrowser.GetContentAsync(sess.Page, Selector, InnerHtml.IsPresent, AsText.IsPresent, Timeout, token).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(OutFile)) {
                string outPath = OutFile!.ToFullPath();
#if NETSTANDARD2_0 || NETFRAMEWORK
                File.WriteAllText(outPath, html);
#else
                await File.WriteAllTextAsync(outPath, html, token).ConfigureAwait(false);
#endif
            } else {
                WriteObject(html);
            }
        }
    }

    private void ApplyRenderProfile() {
        if (RenderProfile != HtmlRenderProfile.HeavyDynamicPage) {
            return;
        }

        if (!MyInvocation.BoundParameters.ContainsKey(nameof(LoadState))) {
            LoadState = HtmlBrowserLoadState.Commit;
        }

        if (string.IsNullOrWhiteSpace(WaitForSelector)
            && string.IsNullOrWhiteSpace(WaitForFunction)
            && WaitAfterLoadMs == 0) {
            WaitAfterLoadMs = 1000;
        }

        if (!MyInvocation.BoundParameters.ContainsKey(nameof(AutoScroll))) {
            AutoScroll = true;
        }

        if (!MyInvocation.BoundParameters.ContainsKey(nameof(AutoScrollSteps))) {
            AutoScrollSteps = 5;
        }

        if (!MyInvocation.BoundParameters.ContainsKey(nameof(BlockResourceType))) {
            BlockResourceType = new[] {
                HtmlNetworkResourceType.Image,
                HtmlNetworkResourceType.Font,
                HtmlNetworkResourceType.Media
            };
        }
    }

    private async Task<string> ReadStaticHtmlAsync(string target, CancellationToken cancellationToken) {
        if (ParameterSetName == ParameterSetFile) {
            return await HtmlUtilities.ReadFileCheckedAsync(Path!.ToFullPath()).ConfigureAwait(false);
        }

        if (Uri.TryCreate(target, UriKind.Absolute, out Uri? uri) && uri.IsFile) {
            return await HtmlUtilities.ReadFileCheckedAsync(uri.LocalPath).ConfigureAwait(false);
        }

        using HttpClient client = HttpClientHelper.Create(Proxy, ProxyCredential, Credential, Username, Password);
        return await HtmlUtilities.GetStringWithProperEncodingAsync(client, target, cancellationToken).ConfigureAwait(false);
    }
}
