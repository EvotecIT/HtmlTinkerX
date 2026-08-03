---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Save-HtmlBrowserContent
## SYNOPSIS
Saves rendered HTML or text content from an active browser session.

## SYNTAX
### Session (Default)
```powershell
Save-HtmlBrowserContent [[-Session] <HtmlBrowserSession>] [-OutFile] <string> [-Selector <string>] [-InnerHtml] [-AsText] [-Timeout <int>] [-PassThru] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

### Url
```powershell
Save-HtmlBrowserContent [-Url] <string> [-OutFile] <string> [-Selector <string>] [-InnerHtml] [-AsText] [-Timeout <int>] [-ProfilePath <string>] [-Scenario <HtmlBrowserScenario>] [-Browser <HtmlBrowserEngine>] [-Clean] [-UserDataDirectory <string>] [-StatePath <string>] [-BrowserChannel <string>] [-Visible] [-Proxy <string>] [-ProxyCredential <pscredential>] [-SlowMo <int>] [-LoadState <HtmlBrowserLoadState>] [-NavigationTimeout <int>] [-BlockResourceType <HtmlNetworkResourceType[]>] [-BlockResourcePattern <string[]>] [-PassThru] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

### File
```powershell
Save-HtmlBrowserContent [-Path] <string> [-OutFile] <string> [-Selector <string>] [-InnerHtml] [-AsText] [-Timeout <int>] [-ProfilePath <string>] [-Scenario <HtmlBrowserScenario>] [-Browser <HtmlBrowserEngine>] [-Clean] [-UserDataDirectory <string>] [-StatePath <string>] [-BrowserChannel <string>] [-Visible] [-Proxy <string>] [-ProxyCredential <pscredential>] [-SlowMo <int>] [-LoadState <HtmlBrowserLoadState>] [-NavigationTimeout <int>] [-BlockResourceType <HtmlNetworkResourceType[]>] [-BlockResourcePattern <string[]>] [-PassThru] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

## DESCRIPTION
Saves rendered HTML or text content from an active browser session.

## EXAMPLES

### EXAMPLE 1
```powershell
$session = Start-HtmlBrowserSession -Url https://example.org/app
Save-HtmlBrowserContent -Session $session -Selector main -OutFile .\rendered-main.html
```


## PARAMETERS

### -AsText
Save text instead of HTML.

```yaml
Type: SwitchParameter
Parameter Sets: Session, Url, File
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
Browser engine to use for URL or file saves.

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
Force re-download of browser runtimes for URL or file saves.

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

### -InnerHtml
Save inner HTML instead of outer HTML.

```yaml
Type: SwitchParameter
Parameter Sets: Session, Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LoadState
Initial browser navigation readiness state for URL or file saves.

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

### -NavigationTimeout
Timeout in milliseconds for one-shot navigation and browser operations.

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

### -OutFile
Output file path.

```yaml
Type: String
Parameter Sets: Session, Url, File
Aliases: None
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PassThru
Write the saved path to the pipeline.

```yaml
Type: SwitchParameter
Parameter Sets: Session, Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
Local HTML file to open for one-shot rendered content saving.

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
Optional browser profile JSON file used as launch defaults for URL or file saves.

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
Proxy server address used when launching the browser for URL or file saves.

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
Credentials used for the proxy server.

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
Intent-focused browser automation defaults used for URL or file saves.

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

### -Selector
Optional selector to save.

```yaml
Type: String
Parameter Sets: Session, Url, File
Aliases: None
Possible values:

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
Accept wildcard characters: False
```

### -StatePath
Playwright storage-state JSON file used for URL or file saves.

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
Timeout in milliseconds while waiting for the selector.

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
URL to open for one-shot rendered content saving.

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
Persistent browser user-data directory used for URL or file saves.

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
Show the browser instead of running headless for URL or file saves.

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

- `System.String`

## RELATED LINKS

- None
