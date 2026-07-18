---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Get-HtmlBrowserSsoHandoff
## SYNOPSIS
Gets SAML, WS-Federation, OAuth, or OpenID Connect form and URL callback handoffs from the current browser page.

## SYNTAX
### Session
```powershell
Get-HtmlBrowserSsoHandoff [[-Session] <HtmlBrowserSession>] [-IncludeSensitiveValues] [-Analyze] [-IncludeXml] [-IncludeJson] [-IncludeAllForms] [-MaxValueLength <int>] [-Wait] [-Timeout <int>] [-PollMilliseconds <int>] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

### Url
```powershell
Get-HtmlBrowserSsoHandoff [-Url] <string> [-Browser <HtmlBrowserEngine>] [-ProfilePath <string>] [-Scenario <HtmlBrowserScenario>] [-UserDataDirectory <string>] [-StatePath <string>] [-BrowserChannel <string>] [-Clean] [-Proxy <string>] [-ProxyCredential <pscredential>] [-Visible] [-SlowMo <int>] [-NavigationTimeout <int>] [-LoadState <HtmlBrowserLoadState>] [-BlockResourceType <HtmlNetworkResourceType[]>] [-BlockResourcePattern <string[]>] [-IncludeSensitiveValues] [-Analyze] [-IncludeXml] [-IncludeJson] [-IncludeAllForms] [-MaxValueLength <int>] [-Wait] [-Timeout <int>] [-PollMilliseconds <int>] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

### File
```powershell
Get-HtmlBrowserSsoHandoff [-Path] <string> [-Browser <HtmlBrowserEngine>] [-ProfilePath <string>] [-Scenario <HtmlBrowserScenario>] [-UserDataDirectory <string>] [-StatePath <string>] [-BrowserChannel <string>] [-Clean] [-Proxy <string>] [-ProxyCredential <pscredential>] [-Visible] [-SlowMo <int>] [-NavigationTimeout <int>] [-LoadState <HtmlBrowserLoadState>] [-BlockResourceType <HtmlNetworkResourceType[]>] [-BlockResourcePattern <string[]>] [-IncludeSensitiveValues] [-Analyze] [-IncludeXml] [-IncludeJson] [-IncludeAllForms] [-MaxValueLength <int>] [-Wait] [-Timeout <int>] [-PollMilliseconds <int>] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

## DESCRIPTION
Gets SAML, WS-Federation, OAuth, or OpenID Connect form and URL callback handoffs from the current browser page.

## EXAMPLES

### EXAMPLE 1
```powershell
$session = Start-HtmlBrowserSession -Url https://portal.contoso.example -Visible -ManualLogin -PreventSsoAutoSubmit
$handoff = Get-HtmlBrowserSsoHandoff -Session $session -Wait -Timeout 60000
$handoff | Select-Object Kind, Action, FormData, SuggestedCommand, Warnings
$analysis = Get-HtmlBrowserSsoHandoff -Session $session -Analyze
$analysis | Select-Object Kind, Action, FieldNames, SamlResponse, JsonWebTokens, Warnings

# Only reveal assertion values when you intentionally need to replay the handoff.
$handoff = Get-HtmlBrowserSsoHandoff -Session $session -IncludeSensitiveValues
$webSession = ConvertTo-HtmlWebRequestSession -Session $session
Invoke-WebRequest -Uri $handoff.Action -Method $handoff.Method -Body $handoff.FormData -WebSession $webSession
```


## PARAMETERS

### -Analyze
Return safe SAML, OAuth, or OpenID Connect protocol analysis instead of raw handoff form data.

```yaml
Type: SwitchParameter
Parameter Sets: Session, Url, File
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
Parameter Sets: Url, File
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
Parameter Sets: Url, File
Aliases: None
Possible values: Document, Stylesheet, Image, Media, Font, Script, TextTrack, XHR, Fetch, EventSource, WebSocket, Manifest, Other

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Browser
Browser engine to use when loading Url or Path.

```yaml
Type: HtmlBrowserEngine
Parameter Sets: Url, File
Aliases: None
Possible values: Chromium, Firefox, WebKit

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
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
Accept wildcard characters: True
```

### -CancellationToken
Token used to cancel the operation.

```yaml
Type: CancellationToken
Parameter Sets: Session, Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Clean
Reinstall browser runtimes when using Url or Path.

```yaml
Type: SwitchParameter
Parameter Sets: Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -IncludeAllForms
Return all forms, not only forms with recognizable SSO handoff fields. URL callbacks still require recognizable protocol fields.

```yaml
Type: SwitchParameter
Parameter Sets: Session, Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -IncludeJson
Include decoded JWT header and payload JSON in analysis output. Sensitive payload values remain redacted unless IncludeSensitiveValues is also set.

```yaml
Type: SwitchParameter
Parameter Sets: Session, Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -IncludeSensitiveValues
Include sensitive assertion, token, and state values. By default those values are redacted.

```yaml
Type: SwitchParameter
Parameter Sets: Session, Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -IncludeXml
Include decoded SAML XML in analysis output. Sensitive XML values remain redacted unless IncludeSensitiveValues is also set.

```yaml
Type: SwitchParameter
Parameter Sets: Session, Url, File
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
Parameter Sets: Url, File
Aliases: None
Possible values: Commit, DomContentLoaded, Load, NetworkIdle

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -MaxValueLength
Maximum field value length to return. Zero disables truncation.

```yaml
Type: Int32
Parameter Sets: Session, Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -NavigationTimeout
Timeout in milliseconds for the initial browser navigation when using Url or Path.

```yaml
Type: Int32
Parameter Sets: Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Path
Path to a local HTML file containing an SSO handoff page.

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

### -PollMilliseconds
Polling interval in milliseconds while waiting for a handoff form.

```yaml
Type: Int32
Parameter Sets: Session, Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
```

### -ProxyCredential
Credentials used for the Proxy server.

```yaml
Type: PSCredential
Parameter Sets: Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
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
Accept wildcard characters: True
```

### -Session
Existing browser session. When omitted, the default PSParseHTML session is used.

```yaml
Type: HtmlBrowserSession
Parameter Sets: Session
Aliases: None
Possible values:

Required: False
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: True
```

### -SlowMo
Slow down Playwright actions by the specified milliseconds.

```yaml
Type: Int32
Parameter Sets: Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
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
Accept wildcard characters: True
```

### -Timeout
Maximum time in milliseconds to wait when Wait is used. Zero waits indefinitely.

```yaml
Type: Int32
Parameter Sets: Session, Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Url
URL of the SSO-protected page or handoff page to inspect.

```yaml
Type: String
Parameter Sets: Url
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
```

### -Wait
Wait until at least one matching SSO handoff form or URL callback is observed.

```yaml
Type: SwitchParameter
Parameter Sets: Session, Url, File
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

- `HtmlTinkerX.HtmlBrowserSession`

## OUTPUTS

- `HtmlTinkerX.HtmlBrowserSsoHandoff
HtmlTinkerX.HtmlSsoHandoffAnalysis`

## RELATED LINKS

- None
