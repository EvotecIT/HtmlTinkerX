---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Get-HtmlBrowserLoginForm
## SYNOPSIS
Cmdlet that detects a login form and returns the selectors needed for form authentication.

## SYNTAX
### Session (Default)
```powershell
Get-HtmlBrowserLoginForm [[-Session] <HtmlBrowserSession>] [-SlowMo <int>] [-Timeout <int>] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

### Url
```powershell
Get-HtmlBrowserLoginForm [-Url] <string> [-Browser <HtmlBrowserEngine>] [-ProfilePath <string>] [-Scenario <HtmlBrowserScenario>] [-UserDataDirectory <string>] [-StatePath <string>] [-BrowserChannel <string>] [-Clean] [-Visible] [-Proxy <string>] [-ProxyCredential <pscredential>] [-SlowMo <int>] [-Timeout <int>] [-LoadState <HtmlBrowserLoadState>] [-BlockResourceType <HtmlNetworkResourceType[]>] [-BlockResourcePattern <string[]>] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

### File
```powershell
Get-HtmlBrowserLoginForm [-Path] <string> [-Browser <HtmlBrowserEngine>] [-ProfilePath <string>] [-Scenario <HtmlBrowserScenario>] [-UserDataDirectory <string>] [-StatePath <string>] [-BrowserChannel <string>] [-Clean] [-Visible] [-Proxy <string>] [-ProxyCredential <pscredential>] [-SlowMo <int>] [-Timeout <int>] [-LoadState <HtmlBrowserLoadState>] [-BlockResourceType <HtmlNetworkResourceType[]>] [-BlockResourcePattern <string[]>] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

## DESCRIPTION
Cmdlet that detects a login form and returns the selectors needed for form authentication.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-HtmlBrowserLoginForm -Path 'C:\Path'
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
Browser engine used when loading Url or Path.

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
Accept wildcard characters: False
```

### -Clean
Force re-download of browser runtimes.

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
Proxy server address.

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

### -Session
Existing browser session.

```yaml
Type: HtmlBrowserSession
Parameter Sets: Session
Aliases: None
Possible values:

Required: False
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -SlowMo
Delay in milliseconds between actions.

```yaml
Type: Int32
Parameter Sets: Session, Url, File
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

### -Timeout
Timeout in milliseconds for browser operations.

```yaml
Type: Int32
Parameter Sets: Session, Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Url
URL to inspect.

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

- `HtmlTinkerX.HtmlBrowserSession`

## OUTPUTS

- `HtmlTinkerX.HtmlFormLogin`

## RELATED LINKS

- None
