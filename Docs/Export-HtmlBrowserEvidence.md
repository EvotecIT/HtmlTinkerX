---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Export-HtmlBrowserEvidence
## SYNOPSIS
Exports screenshot, rendered content, text, Markdown, and optional network evidence from a browser session or URL.

## SYNTAX
### Session (Default)
```powershell
Export-HtmlBrowserEvidence [[-Session] <HtmlBrowserSession>] [-OutFolder] <string> [-BaseFileName <string>] [-Artifact <string[]>] [-Screenshot] [-FullPageScreenshot] [-Pdf] [-Html] [-VisibleText] [-Markdown] [-NetworkSummary] [-SsoHandoffSummary] [-NoManifest] [-NoRedaction] [-NoScreenshotMask] [-ScreenshotMaskSelector <string[]>] [-ScreenshotMaskColor <string>] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

### Url
```powershell
Export-HtmlBrowserEvidence [-Url] <string> [-OutFolder] <string> [-BaseFileName <string>] [-Artifact <string[]>] [-Screenshot] [-FullPageScreenshot] [-Pdf] [-Html] [-VisibleText] [-Markdown] [-NetworkSummary] [-SsoHandoffSummary] [-NoManifest] [-NoRedaction] [-NoScreenshotMask] [-ScreenshotMaskSelector <string[]>] [-ScreenshotMaskColor <string>] [-ProfilePath <string>] [-UserDataDirectory <string>] [-StatePath <string>] [-Browser <HtmlBrowserEngine>] [-Scenario <HtmlBrowserScenario>] [-BrowserChannel <string>] [-Visible] [-PreventSsoAutoSubmit] [-Proxy <string>] [-ProxyCredential <pscredential>] [-LoadState <HtmlBrowserLoadState>] [-Timeout <int>] [-BlockResourceType <HtmlNetworkResourceType[]>] [-BlockResourcePattern <string[]>] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

### File
```powershell
Export-HtmlBrowserEvidence [-Path] <string> [-OutFolder] <string> [-BaseFileName <string>] [-Artifact <string[]>] [-Screenshot] [-FullPageScreenshot] [-Pdf] [-Html] [-VisibleText] [-Markdown] [-NetworkSummary] [-SsoHandoffSummary] [-NoManifest] [-NoRedaction] [-NoScreenshotMask] [-ScreenshotMaskSelector <string[]>] [-ScreenshotMaskColor <string>] [-ProfilePath <string>] [-UserDataDirectory <string>] [-StatePath <string>] [-Browser <HtmlBrowserEngine>] [-Scenario <HtmlBrowserScenario>] [-BrowserChannel <string>] [-Visible] [-PreventSsoAutoSubmit] [-Proxy <string>] [-ProxyCredential <pscredential>] [-LoadState <HtmlBrowserLoadState>] [-Timeout <int>] [-BlockResourceType <HtmlNetworkResourceType[]>] [-BlockResourcePattern <string[]>] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

## DESCRIPTION
Exports screenshot, rendered content, text, Markdown, and optional network evidence from a browser session or URL.

## EXAMPLES

### EXAMPLE 1
```powershell
$session | Export-HtmlBrowserEvidence -OutFolder .\evidence -NetworkSummary
```


### EXAMPLE 2
```powershell
Export-HtmlBrowserEvidence -Url https://example.org -OutFolder .\evidence -Artifact Screenshot,Html,Text,NetworkSummary
```


## PARAMETERS

### -Artifact
Specific artifacts to export. When omitted, screenshot, HTML, text, and Markdown are written, with optional additive switches.

```yaml
Type: String[]
Parameter Sets: Session, Url, File
Aliases: None
Possible values: Screenshot, FullPageScreenshot, Pdf, Html, Text, Markdown, NetworkSummary, SsoHandoffSummary

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -BaseFileName
Base file name for page-level artifacts. The default is page.

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
Browser engine used for URL or file captures.

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

### -FullPageScreenshot
Add a full-page screenshot to the default evidence pack.

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

### -Html
Write rendered page HTML. This is included in the default evidence pack.

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
Initial browser navigation readiness state for URL or file captures.

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

### -Markdown
Write Markdown converted from the rendered page HTML. This is included in the default evidence pack.

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

### -NetworkSummary
Add a redacted network summary without headers or bodies to the default evidence pack.

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

### -NoManifest
Do not write evidence-manifest.json.

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

### -NoRedaction
Write raw text artifacts and manifest URLs without sensitive-value redaction.

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

### -NoScreenshotMask
Do not mask common sensitive fields in visual artifacts such as screenshots and PDFs.

```yaml
Type: SwitchParameter
Parameter Sets: Session, Url, File
Aliases: NoVisualMask
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -OutFolder
Output folder for evidence artifacts.

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

### -Path
Local HTML file to open for one-shot evidence capture.

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

### -Pdf
Add a PDF print of the page to the default evidence pack. Playwright supports this only for Chromium.

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

### -PreventSsoAutoSubmit
Prevent recognized SSO handoff forms from auto-submitting during one-shot URL or file captures.

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
Optional browser profile JSON file used as launch defaults for URL or file captures.

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
Proxy server address used when launching the browser for URL or file captures.

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
Intent-focused browser automation defaults used for URL or file captures.

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

### -Screenshot
Capture the viewport screenshot. This is included in the default evidence pack.

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

### -ScreenshotMaskColor
CSS color used for visual artifact masks.

```yaml
Type: String
Parameter Sets: Session, Url, File
Aliases: VisualMaskColor
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ScreenshotMaskSelector
Additional selectors to mask in visual artifacts such as screenshots and PDFs.

```yaml
Type: String[]
Parameter Sets: Session, Url, File
Aliases: VisualMaskSelector
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Session
Browser session to capture. When omitted, the default PSParseHTML session is used.

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

### -SsoHandoffSummary
Add a redacted SSO handoff summary to the default evidence pack.

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

### -StatePath
Playwright storage-state JSON file used for URL or file captures.

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

### -Url
URL to open for one-shot evidence capture.

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
Persistent browser user-data directory used for URL or file captures.

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
Show the browser instead of running headless for URL or file captures.

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

### -VisibleText
Write visible page text. This is included in the default evidence pack.

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `HtmlTinkerX.HtmlBrowserSession`

## OUTPUTS

- `HtmlTinkerX.HtmlBrowserEvidenceResult`

## RELATED LINKS

- None
