---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Save-HtmlBrowserScreenshot
## SYNOPSIS
Cmdlet that captures a screenshot of a web page using a headless browser. If OutFile has no extension, one is added based on Format.

## SYNTAX
### SessionDefault (Default)
```powershell
Save-HtmlBrowserScreenshot [[-Session] <HtmlBrowserSession>] [[-OutFile] <string>] [-Open] [-Full] [-Delay <int>] [-Selector <string>] [-ElementSelector <string>] [-HighlightSelector <string[]>] [-MaskSelector <string[]>] [-MaskSensitiveElement] [-MaskColor <string>] [-OverlayText <string>] [-Format <ImageFormat>] [-Quality <int>] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

### Default
```powershell
Save-HtmlBrowserScreenshot [-Url] <string> [[-OutFile] <string>] [-Browser <HtmlBrowserEngine>] [-Clean] [-ProfilePath <string>] [-Scenario <HtmlBrowserScenario>] [-UserDataDirectory <string>] [-StatePath <string>] [-BrowserChannel <string>] [-Proxy <string>] [-ProxyCredential <pscredential>] [-Visible] [-SlowMo <int>] [-LoadState <HtmlBrowserLoadState>] [-Timeout <int>] [-BlockResourceType <HtmlNetworkResourceType[]>] [-BlockResourcePattern <string[]>] [-Open] [-Full] [-Delay <int>] [-Selector <string>] [-ElementSelector <string>] [-HighlightSelector <string[]>] [-MaskSelector <string[]>] [-MaskSensitiveElement] [-MaskColor <string>] [-OverlayText <string>] [-Format <ImageFormat>] [-Quality <int>] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

### Clip
```powershell
Save-HtmlBrowserScreenshot [-Url] <string> [[-OutFile] <string>] -X <int> -Y <int> -Width <int> -Height <int> [-Browser <HtmlBrowserEngine>] [-Clean] [-ProfilePath <string>] [-Scenario <HtmlBrowserScenario>] [-UserDataDirectory <string>] [-StatePath <string>] [-BrowserChannel <string>] [-Proxy <string>] [-ProxyCredential <pscredential>] [-Visible] [-SlowMo <int>] [-LoadState <HtmlBrowserLoadState>] [-Timeout <int>] [-BlockResourceType <HtmlNetworkResourceType[]>] [-BlockResourcePattern <string[]>] [-Open] [-Delay <int>] [-Selector <string>] [-ElementSelector <string>] [-HighlightSelector <string[]>] [-MaskSelector <string[]>] [-MaskSensitiveElement] [-MaskColor <string>] [-OverlayText <string>] [-Format <ImageFormat>] [-Quality <int>] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

### FileDefault
```powershell
Save-HtmlBrowserScreenshot [-Path] <string> [[-OutFile] <string>] [-Browser <HtmlBrowserEngine>] [-Clean] [-ProfilePath <string>] [-Scenario <HtmlBrowserScenario>] [-UserDataDirectory <string>] [-StatePath <string>] [-BrowserChannel <string>] [-Proxy <string>] [-ProxyCredential <pscredential>] [-Visible] [-SlowMo <int>] [-LoadState <HtmlBrowserLoadState>] [-Timeout <int>] [-BlockResourceType <HtmlNetworkResourceType[]>] [-BlockResourcePattern <string[]>] [-Open] [-Full] [-Delay <int>] [-Selector <string>] [-ElementSelector <string>] [-HighlightSelector <string[]>] [-MaskSelector <string[]>] [-MaskSensitiveElement] [-MaskColor <string>] [-OverlayText <string>] [-Format <ImageFormat>] [-Quality <int>] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

### FileClip
```powershell
Save-HtmlBrowserScreenshot [-Path] <string> [[-OutFile] <string>] -X <int> -Y <int> -Width <int> -Height <int> [-Browser <HtmlBrowserEngine>] [-Clean] [-ProfilePath <string>] [-Scenario <HtmlBrowserScenario>] [-UserDataDirectory <string>] [-StatePath <string>] [-BrowserChannel <string>] [-Proxy <string>] [-ProxyCredential <pscredential>] [-Visible] [-SlowMo <int>] [-LoadState <HtmlBrowserLoadState>] [-Timeout <int>] [-BlockResourceType <HtmlNetworkResourceType[]>] [-BlockResourcePattern <string[]>] [-Open] [-Delay <int>] [-Selector <string>] [-ElementSelector <string>] [-HighlightSelector <string[]>] [-MaskSelector <string[]>] [-MaskSensitiveElement] [-MaskColor <string>] [-OverlayText <string>] [-Format <ImageFormat>] [-Quality <int>] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

### SessionClip
```powershell
Save-HtmlBrowserScreenshot [[-Session] <HtmlBrowserSession>] [[-OutFile] <string>] -X <int> -Y <int> -Width <int> -Height <int> [-Open] [-Delay <int>] [-Selector <string>] [-ElementSelector <string>] [-HighlightSelector <string[]>] [-MaskSelector <string[]>] [-MaskSensitiveElement] [-MaskColor <string>] [-OverlayText <string>] [-Format <ImageFormat>] [-Quality <int>] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

## DESCRIPTION
Cmdlet that captures a screenshot of a web page using a headless browser. If OutFile has no extension, one is added based on Format.

## EXAMPLES

### EXAMPLE 1
```powershell
Save-HtmlScreenshot -Url https://example.com -OutFile page.png
```


## PARAMETERS

### -BlockResourcePattern
Playwright URL glob patterns to abort before navigation, such as **/analytics/**.

```yaml
Type: String[]
Parameter Sets: Default, Clip, FileDefault, FileClip
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
Parameter Sets: Default, Clip, FileDefault, FileClip
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
Parameter Sets: Default, Clip, FileDefault, FileClip
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
Parameter Sets: Default, Clip, FileDefault, FileClip
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
Parameter Sets: SessionDefault, Default, Clip, FileDefault, FileClip, SessionClip
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
Parameter Sets: Default, Clip, FileDefault, FileClip
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Delay
Milliseconds to wait after the page loads.

```yaml
Type: Int32
Parameter Sets: SessionDefault, Default, Clip, FileDefault, FileClip, SessionClip
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ElementSelector
CSS selector of an element to capture.

```yaml
Type: String
Parameter Sets: SessionDefault, Default, Clip, FileDefault, FileClip, SessionClip
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Format
Image format for the screenshot.

```yaml
Type: ImageFormat
Parameter Sets: SessionDefault, Default, Clip, FileDefault, FileClip, SessionClip
Aliases: None
Possible values: Png, Jpeg, Bmp, Gif

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Full
Capture the entire page.

```yaml
Type: SwitchParameter
Parameter Sets: SessionDefault, Default, FileDefault
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Height
Height of the clip region.

```yaml
Type: Int32
Parameter Sets: Clip, FileClip, SessionClip
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -HighlightSelector
CSS selectors of elements to highlight.

```yaml
Type: String[]
Parameter Sets: SessionDefault, Default, Clip, FileDefault, FileClip, SessionClip
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LoadState
Initial browser navigation readiness state for URL or file captures.

```yaml
Type: HtmlBrowserLoadState
Parameter Sets: Default, Clip, FileDefault, FileClip
Aliases: None
Possible values: Commit, DomContentLoaded, Load, NetworkIdle

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaskColor
CSS color used for masked elements.

```yaml
Type: String
Parameter Sets: SessionDefault, Default, Clip, FileDefault, FileClip, SessionClip
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaskSelector
CSS selectors of elements to mask.

```yaml
Type: String[]
Parameter Sets: SessionDefault, Default, Clip, FileDefault, FileClip, SessionClip
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaskSensitiveElement
Mask common sensitive fields such as password, token, SAML, MFA, OTP, and secret inputs.

```yaml
Type: SwitchParameter
Parameter Sets: SessionDefault, Default, Clip, FileDefault, FileClip, SessionClip
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Open
Open the screenshot after saving.

```yaml
Type: SwitchParameter
Parameter Sets: SessionDefault, Default, Clip, FileDefault, FileClip, SessionClip
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -OutFile
File path for the screenshot.

```yaml
Type: String
Parameter Sets: SessionDefault, Default, Clip, FileDefault, FileClip, SessionClip
Aliases: None
Possible values:

Required: False
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -OverlayText
Text to overlay on the screenshot.

```yaml
Type: String
Parameter Sets: SessionDefault, Default, Clip, FileDefault, FileClip, SessionClip
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
Parameter Sets: FileDefault, FileClip
Aliases: File
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ProfilePath
Optional browser profile JSON file used as launch defaults for URL or file captures.

```yaml
Type: String
Parameter Sets: Default, Clip, FileDefault, FileClip
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
Parameter Sets: Default, Clip, FileDefault, FileClip
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
Parameter Sets: Default, Clip, FileDefault, FileClip
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Quality
Encoder quality for JPEG output.

```yaml
Type: Int32
Parameter Sets: SessionDefault, Default, Clip, FileDefault, FileClip, SessionClip
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Scenario
Intent-focused browser automation defaults used for URL or file captures.

```yaml
Type: HtmlBrowserScenario
Parameter Sets: Default, Clip, FileDefault, FileClip
Aliases: None
Possible values: Custom, AuditProof, MailboxProof, LoginProtected, SinglePageApp, LowBandwidth, NetworkCapture, DownloadEvidence

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Selector
CSS selector to wait for before capturing.

```yaml
Type: String
Parameter Sets: SessionDefault, Default, Clip, FileDefault, FileClip, SessionClip
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
Parameter Sets: SessionDefault, SessionClip
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
Parameter Sets: Default, Clip, FileDefault, FileClip
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -StatePath
Playwright storage-state JSON file used for URL or file captures.

```yaml
Type: String
Parameter Sets: Default, Clip, FileDefault, FileClip
Aliases: StorageStatePath
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
Parameter Sets: Default, Clip, FileDefault, FileClip
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
Parameter Sets: Default, Clip
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -UserDataDirectory
Persistent browser user-data directory used for URL or file captures.

```yaml
Type: String
Parameter Sets: Default, Clip, FileDefault, FileClip
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
Parameter Sets: Default, Clip, FileDefault, FileClip
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Width
Width of the clip region.

```yaml
Type: Int32
Parameter Sets: Clip, FileClip, SessionClip
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -X
X coordinate for a clip region.

```yaml
Type: Int32
Parameter Sets: Clip, FileClip, SessionClip
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Y
Y coordinate for a clip region.

```yaml
Type: Int32
Parameter Sets: Clip, FileClip, SessionClip
Aliases: None
Possible values:

Required: True
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

- `None`

## RELATED LINKS

- None
