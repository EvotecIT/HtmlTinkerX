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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `HtmlTinkerX.HtmlBrowserSession`

## OUTPUTS

- `System.Object`

## RELATED LINKS

- None
