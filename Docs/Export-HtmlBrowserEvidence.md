---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/PSParseHTML
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `HtmlTinkerX.HtmlBrowserSession`

## OUTPUTS

- `HtmlTinkerX.HtmlBrowserEvidenceResult`

## RELATED LINKS

- None
