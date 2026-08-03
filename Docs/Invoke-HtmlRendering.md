---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Invoke-HtmlRendering
## SYNOPSIS
Cmdlet that retrieves HTML content after executing JavaScript using a headless browser.

## SYNTAX
### Default (Default)
```powershell
Invoke-HtmlRendering [-Url] <string> [-OutFile <string>] [-Browser <HtmlBrowserEngine>] [-Clean] [-Proxy <string>] [-ProxyCredential <pscredential>] [-Credential <pscredential>] [-Username <string>] [-Password <string>] [-LoginUrl <string>] [-UsernameSelector <string>] [-PasswordSelector <string>] [-SubmitSelector <string>] [-Session] [-StorageStatePath <string>] [-Selector <string>] [-InnerHtml] [-AsText] [-Snapshot] [-IncludeNetworkLog] [-RenderProfile <HtmlRenderProfile>] [-IncludeStaticRenderedComparison] [-IncludeLinkedScripts] [-IncludeExternalLinkedScripts] [-LinkedScriptMaximumResponseBytes <int>] [-IncludeResponseBody] [-RedactResponseBody] [-ResponseBodyMaxBytes <int>] [-ResponseBodyResourceType <HtmlNetworkResourceType[]>] [-WaitForSelector <string>] [-WaitForFunction <string>] [-LoadState <HtmlBrowserLoadState>] [-BlockResourceType <HtmlNetworkResourceType[]>] [-BlockResourcePattern <string[]>] [-ClickSelector <string[]>] [-ClickText <string[]>] [-DismissSelector <string[]>] [-DismissText <string[]>] [-InteractionDelayMs <int>] [-InteractionRepeatCount <int>] [-WaitAfterLoadMs <int>] [-AutoScroll] [-AutoScrollSteps <int>] [-AutoScrollDelayMs <int>] [-NoDefault] [-Visible] [-SlowMo <int>] [-Timeout <int>] [-CancellationToken <CancellationToken>] [-UserAgent <string>] [-ViewportWidth <Int32>] [-ViewportHeight <Int32>] [-DeviceScaleFactor <Double>] [-GeoLatitude <Double>] [-GeoLongitude <Double>] [-Timezone <string>] [<CommonParameters>]
```

### File
```powershell
Invoke-HtmlRendering [-Path] <string> [-OutFile <string>] [-Browser <HtmlBrowserEngine>] [-Clean] [-Proxy <string>] [-ProxyCredential <pscredential>] [-Credential <pscredential>] [-Username <string>] [-Password <string>] [-LoginUrl <string>] [-UsernameSelector <string>] [-PasswordSelector <string>] [-SubmitSelector <string>] [-Session] [-StorageStatePath <string>] [-Selector <string>] [-InnerHtml] [-AsText] [-Snapshot] [-IncludeNetworkLog] [-RenderProfile <HtmlRenderProfile>] [-IncludeStaticRenderedComparison] [-IncludeLinkedScripts] [-IncludeExternalLinkedScripts] [-LinkedScriptMaximumResponseBytes <int>] [-IncludeResponseBody] [-RedactResponseBody] [-ResponseBodyMaxBytes <int>] [-ResponseBodyResourceType <HtmlNetworkResourceType[]>] [-WaitForSelector <string>] [-WaitForFunction <string>] [-LoadState <HtmlBrowserLoadState>] [-BlockResourceType <HtmlNetworkResourceType[]>] [-BlockResourcePattern <string[]>] [-ClickSelector <string[]>] [-ClickText <string[]>] [-DismissSelector <string[]>] [-DismissText <string[]>] [-InteractionDelayMs <int>] [-InteractionRepeatCount <int>] [-WaitAfterLoadMs <int>] [-AutoScroll] [-AutoScrollSteps <int>] [-AutoScrollDelayMs <int>] [-NoDefault] [-Visible] [-SlowMo <int>] [-Timeout <int>] [-CancellationToken <CancellationToken>] [-UserAgent <string>] [-ViewportWidth <Int32>] [-ViewportHeight <Int32>] [-DeviceScaleFactor <Double>] [-GeoLatitude <Double>] [-GeoLongitude <Double>] [-Timezone <string>] [<CommonParameters>]
```

## DESCRIPTION
Cmdlet that retrieves HTML content after executing JavaScript using a headless browser.

## EXAMPLES

### EXAMPLE 1
```powershell
Invoke-HtmlRendering -Url https://example.com -Browser Chromium -Clean
```


## PARAMETERS

### -AsText
Return rendered text instead of HTML markup.

```yaml
Type: SwitchParameter
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -AutoScroll
Scroll the rendered page before extraction to trigger lazy-loaded content.

```yaml
Type: SwitchParameter
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -AutoScrollDelayMs
Delay after each auto-scroll step in milliseconds.

```yaml
Type: Int32
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -AutoScrollSteps
Number of incremental scroll steps performed when AutoScroll is enabled.

```yaml
Type: Int32
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -BlockResourcePattern
Playwright URL glob patterns to abort before navigation, such as **/analytics/**.

```yaml
Type: String[]
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -BlockResourceType
Browser resource types to abort before navigation, such as Image, Media, Font, or Stylesheet.

```yaml
Type: HtmlNetworkResourceType[]
Parameter Sets: Default, File
Aliases: None
Possible values: Document, Stylesheet, Image, Media, Font, Script, TextTrack, XHR, Fetch, EventSource, WebSocket, Manifest, Other

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Browser
Browser engine to use for rendering.

```yaml
Type: HtmlBrowserEngine
Parameter Sets: Default, File
Aliases: None
Possible values: Chromium, Firefox, WebKit

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -CancellationToken
Token used to cancel the operation.

```yaml
Type: CancellationToken
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Clean
Force re-download of browser runtimes.

```yaml
Type: SwitchParameter
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ClickSelector
Optional selectors to click before extraction.

```yaml
Type: String[]
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ClickText
Optional visible texts to click before extraction.

```yaml
Type: String[]
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Credential
Credentials used when accessing authenticated pages.

```yaml
Type: PSCredential
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DeviceScaleFactor
Scaling factor for high DPI devices.

```yaml
Type: Double
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DismissSelector
Optional selectors to dismiss before normal click interactions.

```yaml
Type: String[]
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DismissText
Optional visible texts to dismiss before normal click interactions.

```yaml
Type: String[]
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -GeoLatitude
Latitude used for geolocation.

```yaml
Type: Double
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -GeoLongitude
Longitude used for geolocation.

```yaml
Type: Double
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeExternalLinkedScripts
Allow cross-origin linked JavaScript downloads when IncludeLinkedScripts is used.

```yaml
Type: SwitchParameter
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeLinkedScripts
Download and inspect same-origin linked JavaScript files for endpoint discovery in Snapshot output.

```yaml
Type: SwitchParameter
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeNetworkLog
Include browser network entries in Snapshot output. Headers may contain sensitive values.

```yaml
Type: SwitchParameter
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeResponseBody
Capture response bodies for selected network requests in Snapshot output. Bodies may contain sensitive values.

```yaml
Type: SwitchParameter
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeStaticRenderedComparison
Include a static-vs-rendered comparison in Snapshot output.

```yaml
Type: SwitchParameter
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -InnerHtml
Return inner HTML for the selected element instead of outer HTML.

```yaml
Type: SwitchParameter
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -InteractionDelayMs
Delay after each rendered interaction in milliseconds.

```yaml
Type: Int32
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -InteractionRepeatCount
Number of times click interactions should be retried on rendered pages.

```yaml
Type: Int32
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LinkedScriptMaximumResponseBytes
Maximum number of bytes accepted for each linked JavaScript response included in Snapshot discovery.

```yaml
Type: Int32
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LoadState
Initial browser navigation readiness state.

```yaml
Type: HtmlBrowserLoadState
Parameter Sets: Default, File
Aliases: None
Possible values: Commit, DomContentLoaded, Load, NetworkIdle

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LoginUrl
URL for login form when using form authentication.

```yaml
Type: String
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -NoDefault
Do not set the opened session as the default session.

```yaml
Type: SwitchParameter
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -OutFile
Optional file path to save the rendered HTML.

```yaml
Type: String
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Password
Password for pages secured with basic authentication.

```yaml
Type: String
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PasswordSelector
CSS selector for the password field of the login form.

```yaml
Type: String
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
Path to a local HTML file.

```yaml
Type: String
Parameter Sets: File
Aliases: File
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Proxy
Proxy server address used when launching the browser.

```yaml
Type: String
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ProxyCredential
Credentials used for the Proxy server.

```yaml
Type: PSCredential
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RedactResponseBody
Redact common sensitive values from captured response body text.

```yaml
Type: SwitchParameter
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RenderProfile
Preset rendering strategy for common dynamic-page scenarios.

```yaml
Type: HtmlRenderProfile
Parameter Sets: Default, File
Aliases: None
Possible values: Custom, HeavyDynamicPage, FastStaticFallback, InteractivePage, LazyLoadedPage, LazyLoadedContent, AppShell, LoginProtected, NetworkCapture, LowBandwidth

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ResponseBodyMaxBytes
Maximum UTF-8 bytes stored per captured response body.

```yaml
Type: Int32
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ResponseBodyResourceType
Network resource types whose response bodies should be captured. Defaults to XHR and Fetch.

```yaml
Type: HtmlNetworkResourceType[]
Parameter Sets: Default, File
Aliases: None
Possible values: Document, Stylesheet, Image, Media, Font, Script, TextTrack, XHR, Fetch, EventSource, WebSocket, Manifest, Other

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Selector
Optional CSS selector used to return one rendered element instead of the full document.

```yaml
Type: String
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Session
Return a browser session instead of HTML.

```yaml
Type: SwitchParameter
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SlowMo
Slow down Playwright actions by the specified milliseconds.

```yaml
Type: Int32
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Snapshot
Return a structured rendered-page snapshot with common parsed app data instead of raw content.

```yaml
Type: SwitchParameter
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -StorageStatePath
Optional Playwright storage state file used to reuse cookies, local storage, and authenticated browser state.

```yaml
Type: String
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SubmitSelector
CSS selector for the submit element of the login form.

```yaml
Type: String
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Timeout
Timeout in milliseconds for browser operations.

```yaml
Type: Int32
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Timezone
Timezone identifier.

```yaml
Type: String
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Url
URL of the web page.

```yaml
Type: String
Parameter Sets: Default
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -UserAgent
User agent string used when launching the browser.

```yaml
Type: String
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Username
Username for pages secured with basic authentication.

```yaml
Type: String
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -UsernameSelector
CSS selector for the username field of the login form.

```yaml
Type: String
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ViewportHeight
Viewport height in pixels.

```yaml
Type: Int32
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ViewportWidth
Viewport width in pixels.

```yaml
Type: Int32
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Visible
Show the browser instead of running headless.

```yaml
Type: SwitchParameter
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -WaitAfterLoadMs
Optional delay after rendered page load in milliseconds.

```yaml
Type: Int32
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -WaitForFunction
Optional JavaScript predicate to wait for before extracting rendered content.

```yaml
Type: String
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -WaitForSelector
Optional CSS selector to wait for before extracting rendered content.

```yaml
Type: String
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `None`

## OUTPUTS

- `System.String`
- `HtmlTinkerX.HtmlBrowserSession`
- `HtmlTinkerX.HtmlRenderedPageSnapshot`

## RELATED LINKS

- None
