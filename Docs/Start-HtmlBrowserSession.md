---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Start-HtmlBrowserSession
## SYNOPSIS
Starts a browser session for rendered web automation, extraction, and evidence workflows.

## SYNTAX
### Url (Default)
```powershell
Start-HtmlBrowserSession [-Url] <string> [-ProfilePath <string>] [-UserDataDirectory <string>] [-StatePath <string>] [-Browser <HtmlBrowserEngine>] [-Scenario <HtmlBrowserScenario>] [-BrowserChannel <string>] [-BrowserExecutablePath <string>] [-CdpEndpointUrl <string>] [-BrowserArgument <string[]>] [-ChromiumSandbox] [-Clean] [-Visible] [-SlowMo <int>] [-Timeout <int>] [-Credential <pscredential>] [-LoginUrl <string>] [-UsernameSelector <string>] [-PasswordSelector <string>] [-SubmitSelector <string>] [-ManualLogin] [-LoginSuccessSelector <string>] [-LoginTimeout <int>] [-PreventSsoAutoSubmit] [-Username <string>] [-Password <string>] [-Proxy <string>] [-ProxyCredential <pscredential>] [-UserAgent <string>] [-Locale <string>] [-ViewportWidth <Int32>] [-ViewportHeight <Int32>] [-ScreenWidth <Int32>] [-ScreenHeight <Int32>] [-DeviceScaleFactor <Double>] [-Mobile] [-Touch] [-GeoLatitude <Double>] [-GeoLongitude <Double>] [-Timezone <string>] [-Permission <string[]>] [-InitScript <string[]>] [-InitScriptPath <string[]>] [-BlockResourceType <HtmlNetworkResourceType[]>] [-BlockResourcePattern <string[]>] [-LoadState <HtmlBrowserLoadState>] [-NoDefault] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

### File
```powershell
Start-HtmlBrowserSession [-Path] <string> [-ProfilePath <string>] [-UserDataDirectory <string>] [-StatePath <string>] [-Browser <HtmlBrowserEngine>] [-Scenario <HtmlBrowserScenario>] [-BrowserChannel <string>] [-BrowserExecutablePath <string>] [-CdpEndpointUrl <string>] [-BrowserArgument <string[]>] [-ChromiumSandbox] [-Clean] [-Visible] [-SlowMo <int>] [-Timeout <int>] [-Credential <pscredential>] [-LoginUrl <string>] [-UsernameSelector <string>] [-PasswordSelector <string>] [-SubmitSelector <string>] [-ManualLogin] [-LoginSuccessSelector <string>] [-LoginTimeout <int>] [-PreventSsoAutoSubmit] [-Username <string>] [-Password <string>] [-Proxy <string>] [-ProxyCredential <pscredential>] [-UserAgent <string>] [-Locale <string>] [-ViewportWidth <Int32>] [-ViewportHeight <Int32>] [-ScreenWidth <Int32>] [-ScreenHeight <Int32>] [-DeviceScaleFactor <Double>] [-Mobile] [-Touch] [-GeoLatitude <Double>] [-GeoLongitude <Double>] [-Timezone <string>] [-Permission <string[]>] [-InitScript <string[]>] [-InitScriptPath <string[]>] [-BlockResourceType <HtmlNetworkResourceType[]>] [-BlockResourcePattern <string[]>] [-LoadState <HtmlBrowserLoadState>] [-NoDefault] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

## DESCRIPTION
Starts a browser session for rendered web automation, extraction, and evidence workflows.

## EXAMPLES

### EXAMPLE 1
```powershell
Start-HtmlBrowserSession -Url https://example.org -UserDataDirectory .\.profiles\work -BrowserChannel chrome -Visible
```


## PARAMETERS

### -BlockResourcePattern
Playwright URL glob patterns to abort before navigation, such as **/analytics/**.

```yaml
Type: String[]
Parameter Sets: Url, File
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
Parameter Sets: Url, File
Aliases: None
Possible values: Document, Stylesheet, Image, Media, Font, Script, TextTrack, XHR, Fetch, EventSource, WebSocket, Manifest, Other

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Browser
Browser engine to use.

```yaml
Type: HtmlBrowserEngine
Parameter Sets: Url, File
Aliases: None
Possible values: Chromium, Firefox, WebKit

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -BrowserArgument
Additional browser command-line arguments.

```yaml
Type: String[]
Parameter Sets: Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -BrowserChannel
Browser distribution channel, such as chrome, msedge, chromium, chrome-beta, or msedge-dev.

```yaml
Type: String
Parameter Sets: Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -BrowserExecutablePath
Path to a browser executable.

```yaml
Type: String
Parameter Sets: Url, File
Aliases: None
Possible values:

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
Parameter Sets: Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -CdpEndpointUrl
Chrome DevTools Protocol endpoint URL for attaching to an already-running Chromium browser.

```yaml
Type: String
Parameter Sets: Url, File
Aliases: CdpEndpoint, RemoteDebuggingUrl
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ChromiumSandbox
Enable Chromium sandboxing when supported.

```yaml
Type: SwitchParameter
Parameter Sets: Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Clean
Force browser runtime reinstall before launch.

```yaml
Type: SwitchParameter
Parameter Sets: Url, File
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
Parameter Sets: Url, File
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
Parameter Sets: Url, File
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
Parameter Sets: Url, File
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
Parameter Sets: Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -InitScript
JavaScript snippets evaluated before page scripts run.

```yaml
Type: String[]
Parameter Sets: Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -InitScriptPath
JavaScript files evaluated before page scripts run.

```yaml
Type: String[]
Parameter Sets: Url, File
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
Parameter Sets: Url, File
Aliases: None
Possible values: Commit, DomContentLoaded, Load, NetworkIdle

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Locale
Locale used by the browser context, such as en-US or pl-PL.

```yaml
Type: String
Parameter Sets: Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LoginSuccessSelector
CSS selector that indicates manual login completed successfully.

```yaml
Type: String
Parameter Sets: Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LoginTimeout
Timeout in milliseconds used when waiting for LoginSuccessSelector.

```yaml
Type: Int32
Parameter Sets: Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LoginUrl
Login page URL used for form-based authentication before navigating to the requested URL.

```yaml
Type: String
Parameter Sets: Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ManualLogin
Open a visible browser for manual MFA/SSO login and optionally wait for a post-login selector.

```yaml
Type: SwitchParameter
Parameter Sets: Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Mobile
Expose mobile browser behavior where supported.

```yaml
Type: SwitchParameter
Parameter Sets: Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -NoDefault
Do not store this session as the default session.

```yaml
Type: SwitchParameter
Parameter Sets: Url, File
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
Parameter Sets: Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PasswordSelector
CSS selector for the password field used with LoginUrl.

```yaml
Type: String
Parameter Sets: Url, File
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

### -Permission
Browser permissions granted to pages in the context.

```yaml
Type: String[]
Parameter Sets: Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PreventSsoAutoSubmit
Prevent recognized SSO handoff forms from auto-submitting so their hidden assertion fields can be inspected.

```yaml
Type: SwitchParameter
Parameter Sets: Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ProfilePath
Optional browser profile JSON file used as launch defaults.

```yaml
Type: String
Parameter Sets: Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Proxy
Proxy server address used when launching the browser.

```yaml
Type: String
Parameter Sets: Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ProxyCredential
Proxy credentials.

```yaml
Type: PSCredential
Parameter Sets: Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Scenario
Intent-focused browser automation defaults to apply before explicit parameter values.

```yaml
Type: HtmlBrowserScenario
Parameter Sets: Url, File
Aliases: None
Possible values: Custom, AuditProof, MailboxProof, LoginProtected, SinglePageApp, LowBandwidth, NetworkCapture, DownloadEvidence

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ScreenHeight
Screen height in pixels.

```yaml
Type: Int32
Parameter Sets: Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ScreenWidth
Screen width in pixels.

```yaml
Type: Int32
Parameter Sets: Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SlowMo
Delay Playwright actions by the specified milliseconds.

```yaml
Type: Int32
Parameter Sets: Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -StatePath
Playwright storage-state JSON file for cookies and local storage.

```yaml
Type: String
Parameter Sets: Url, File
Aliases: StorageStatePath
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SubmitSelector
CSS selector for the submit button used with LoginUrl.

```yaml
Type: String
Parameter Sets: Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Timeout
Timeout in milliseconds for navigation and browser operations.

```yaml
Type: Int32
Parameter Sets: Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Timezone
Timezone identifier used by the browser JavaScript runtime.

```yaml
Type: String
Parameter Sets: Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Touch
Expose touch input where supported.

```yaml
Type: SwitchParameter
Parameter Sets: Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Url
URL to navigate to.

```yaml
Type: String
Parameter Sets: Url
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -UserAgent
User agent string used by the browser context.

```yaml
Type: String
Parameter Sets: Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -UserDataDirectory
Persistent browser user-data directory for cookies, storage, cache, and permissions.

```yaml
Type: String
Parameter Sets: Url, File
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
Parameter Sets: Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -UsernameSelector
CSS selector for the username field used with LoginUrl.

```yaml
Type: String
Parameter Sets: Url, File
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
Parameter Sets: Url, File
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
Parameter Sets: Url, File
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
Parameter Sets: Url, File
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

- `HtmlTinkerX.HtmlBrowserSession`

## RELATED LINKS

- None
