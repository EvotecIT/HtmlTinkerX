---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/PSParseHTML
schema: 2.0.0
---
# Invoke-HtmlRendering
## SYNOPSIS
Cmdlet that retrieves HTML content after executing JavaScript using a headless browser.

## SYNTAX
### Default (Default)
```powershell
Invoke-HtmlRendering [-Url] <string> [-OutFile <string>] [-Browser <HtmlBrowserEngine>] [-Clean] [-Proxy <string>] [-ProxyCredential <pscredential>] [-Credential <pscredential>] [-Username <string>] [-Password <string>] [-LoginUrl <string>] [-UsernameSelector <string>] [-PasswordSelector <string>] [-SubmitSelector <string>] [-Session] [-StorageStatePath <string>] [-Selector <string>] [-InnerHtml] [-AsText] [-Snapshot] [-IncludeNetworkLog] [-RenderProfile <HtmlRenderProfile>] [-IncludeStaticRenderedComparison] [-IncludeLinkedScripts] [-IncludeExternalLinkedScripts] [-IncludeResponseBody] [-RedactResponseBody] [-ResponseBodyMaxBytes <int>] [-ResponseBodyResourceType <HtmlNetworkResourceType[]>] [-WaitForSelector <string>] [-WaitForFunction <string>] [-LoadState <HtmlBrowserLoadState>] [-BlockResourceType <HtmlNetworkResourceType[]>] [-BlockResourcePattern <string[]>] [-ClickSelector <string[]>] [-ClickText <string[]>] [-DismissSelector <string[]>] [-DismissText <string[]>] [-InteractionDelayMs <int>] [-InteractionRepeatCount <int>] [-WaitAfterLoadMs <int>] [-AutoScroll] [-AutoScrollSteps <int>] [-AutoScrollDelayMs <int>] [-NoDefault] [-Visible] [-SlowMo <int>] [-Timeout <int>] [-CancellationToken <CancellationToken>] [-UserAgent <string>] [-ViewportWidth <int>] [-ViewportHeight <int>] [-DeviceScaleFactor <double>] [-GeoLatitude <double>] [-GeoLongitude <double>] [-Timezone <string>] [<CommonParameters>]
```

### File
```powershell
Invoke-HtmlRendering [-Path] <string> [-OutFile <string>] [-Browser <HtmlBrowserEngine>] [-Clean] [-Proxy <string>] [-ProxyCredential <pscredential>] [-Credential <pscredential>] [-Username <string>] [-Password <string>] [-LoginUrl <string>] [-UsernameSelector <string>] [-PasswordSelector <string>] [-SubmitSelector <string>] [-Session] [-StorageStatePath <string>] [-Selector <string>] [-InnerHtml] [-AsText] [-Snapshot] [-IncludeNetworkLog] [-RenderProfile <HtmlRenderProfile>] [-IncludeStaticRenderedComparison] [-IncludeLinkedScripts] [-IncludeExternalLinkedScripts] [-IncludeResponseBody] [-RedactResponseBody] [-ResponseBodyMaxBytes <int>] [-ResponseBodyResourceType <HtmlNetworkResourceType[]>] [-WaitForSelector <string>] [-WaitForFunction <string>] [-LoadState <HtmlBrowserLoadState>] [-BlockResourceType <HtmlNetworkResourceType[]>] [-BlockResourcePattern <string[]>] [-ClickSelector <string[]>] [-ClickText <string[]>] [-DismissSelector <string[]>] [-DismissText <string[]>] [-InteractionDelayMs <int>] [-InteractionRepeatCount <int>] [-WaitAfterLoadMs <int>] [-AutoScroll] [-AutoScrollSteps <int>] [-AutoScrollDelayMs <int>] [-NoDefault] [-Visible] [-SlowMo <int>] [-Timeout <int>] [-CancellationToken <CancellationToken>] [-UserAgent <string>] [-ViewportWidth <int>] [-ViewportHeight <int>] [-DeviceScaleFactor <double>] [-GeoLatitude <double>] [-GeoLongitude <double>] [-Timezone <string>] [<CommonParameters>]
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
```

### -DeviceScaleFactor
Scaling factor for high DPI devices.

```yaml
Type: Nullable`1
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
```

### -GeoLatitude
Latitude used for geolocation.

```yaml
Type: Nullable`1
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -GeoLongitude
Longitude used for geolocation.

```yaml
Type: Nullable`1
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
```

### -ViewportHeight
Viewport height in pixels.

```yaml
Type: Nullable`1
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -ViewportWidth
Viewport width in pixels.

```yaml
Type: Nullable`1
Parameter Sets: Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `None`

## OUTPUTS

- `System.String
HtmlTinkerX.HtmlBrowserSession
HtmlTinkerX.HtmlRenderedPageSnapshot`

## RELATED LINKS

- None
