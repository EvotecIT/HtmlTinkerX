---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Save-HtmlBrowserPdf
## SYNOPSIS
Cmdlet that generates a PDF from a web page using a headless browser.

## SYNTAX
### Session (Default)
```powershell
Save-HtmlBrowserPdf [[-Session] <HtmlBrowserSession>] [-OutFile] <string> [-SlowMo <int>] [-Open] [-Delay <int>] [-Selector <string>] [-MaskSelector <string[]>] [-MaskSensitiveElement] [-MaskColor <string>] [-Landscape] [-PrintBackground] [-Format <PdfPageFormat>] [-Width <string>] [-Height <string>] [-MarginTop <string>] [-MarginRight <string>] [-MarginBottom <string>] [-MarginLeft <string>] [-PageRanges <string>] [-Scale <Single>] [-DisplayHeaderFooter] [-HeaderTemplate <string>] [-FooterTemplate <string>] [-PreferCssPageSize] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

### Default
```powershell
Save-HtmlBrowserPdf [-Url] <string> [-OutFile] <string> [-Browser <HtmlBrowserEngine>] [-Clean] [-ProfilePath <string>] [-Scenario <HtmlBrowserScenario>] [-UserDataDirectory <string>] [-StatePath <string>] [-BrowserChannel <string>] [-Visible] [-Proxy <string>] [-ProxyCredential <pscredential>] [-SlowMo <int>] [-LoadState <HtmlBrowserLoadState>] [-Timeout <int>] [-BlockResourceType <HtmlNetworkResourceType[]>] [-BlockResourcePattern <string[]>] [-Open] [-Delay <int>] [-Selector <string>] [-MaskSelector <string[]>] [-MaskSensitiveElement] [-MaskColor <string>] [-Landscape] [-PrintBackground] [-Format <PdfPageFormat>] [-Width <string>] [-Height <string>] [-MarginTop <string>] [-MarginRight <string>] [-MarginBottom <string>] [-MarginLeft <string>] [-PageRanges <string>] [-Scale <Single>] [-DisplayHeaderFooter] [-HeaderTemplate <string>] [-FooterTemplate <string>] [-PreferCssPageSize] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

### File
```powershell
Save-HtmlBrowserPdf [-Path] <string> [-OutFile] <string> [-Browser <HtmlBrowserEngine>] [-Clean] [-ProfilePath <string>] [-Scenario <HtmlBrowserScenario>] [-UserDataDirectory <string>] [-StatePath <string>] [-BrowserChannel <string>] [-Visible] [-Proxy <string>] [-ProxyCredential <pscredential>] [-SlowMo <int>] [-LoadState <HtmlBrowserLoadState>] [-Timeout <int>] [-BlockResourceType <HtmlNetworkResourceType[]>] [-BlockResourcePattern <string[]>] [-Open] [-Delay <int>] [-Selector <string>] [-MaskSelector <string[]>] [-MaskSensitiveElement] [-MaskColor <string>] [-Landscape] [-PrintBackground] [-Format <PdfPageFormat>] [-Width <string>] [-Height <string>] [-MarginTop <string>] [-MarginRight <string>] [-MarginBottom <string>] [-MarginLeft <string>] [-PageRanges <string>] [-Scale <Single>] [-DisplayHeaderFooter] [-HeaderTemplate <string>] [-FooterTemplate <string>] [-PreferCssPageSize] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

## DESCRIPTION
Cmdlet that generates a PDF from a web page using a headless browser.

## EXAMPLES

### EXAMPLE 1
```powershell
Save-HTMLPdf -Url https://example.com -OutFile page.pdf
```


### EXAMPLE 2
```powershell
Invoke-HtmlRendering -Url https://example.com -Session |
              Save-HTMLPdf -OutFile page.pdf
```


## PARAMETERS

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

### -BrowserChannel
Browser distribution channel, such as chrome, msedge, chromium, chrome-beta, or msedge-dev.

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

### -CancellationToken
Token used to cancel the operation.

```yaml
Type: CancellationToken
Parameter Sets: Session, Default, File
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

### -Delay
Milliseconds to wait after the page loads.

```yaml
Type: Int32
Parameter Sets: Session, Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DisplayHeaderFooter
Display headers and footers.

```yaml
Type: SwitchParameter
Parameter Sets: Session, Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -FooterTemplate
Footer HTML template.

```yaml
Type: String
Parameter Sets: Session, Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Format
Paper format (e.g. A4).

```yaml
Type: PdfPageFormat
Parameter Sets: Session, Default, File
Aliases: None
Possible values: A0, A1, A2, A3, A4, A5, A6, Letter, Legal, Tabloid, Ledger

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -HeaderTemplate
Header HTML template.

```yaml
Type: String
Parameter Sets: Session, Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Height
Paper height (e.g. 11in).

```yaml
Type: String
Parameter Sets: Session, Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Landscape
Render the page in landscape orientation.

```yaml
Type: SwitchParameter
Parameter Sets: Session, Default, File
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
Parameter Sets: Default, File
Aliases: None
Possible values: Commit, DomContentLoaded, Load, NetworkIdle

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MarginBottom
Bottom margin.

```yaml
Type: String
Parameter Sets: Session, Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MarginLeft
Left margin.

```yaml
Type: String
Parameter Sets: Session, Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MarginRight
Right margin.

```yaml
Type: String
Parameter Sets: Session, Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MarginTop
Top margin.

```yaml
Type: String
Parameter Sets: Session, Default, File
Aliases: None
Possible values:

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
Parameter Sets: Session, Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaskSelector
CSS selectors of elements to mask before generating the PDF.

```yaml
Type: String[]
Parameter Sets: Session, Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaskSensitiveElement
Mask common sensitive fields such as password, token, SAML, MFA, OTP, and secret inputs before generating the PDF.

```yaml
Type: SwitchParameter
Parameter Sets: Session, Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Open
Open the PDF after saving.

```yaml
Type: SwitchParameter
Parameter Sets: Session, Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -OutFile
File path for the PDF.

```yaml
Type: String
Parameter Sets: Session, Default, File
Aliases: None
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PageRanges
Page ranges to print.

```yaml
Type: String
Parameter Sets: Session, Default, File
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

### -PreferCssPageSize
Prefer CSS @page size rules.

```yaml
Type: SwitchParameter
Parameter Sets: Session, Default, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PrintBackground
Include background graphics.

```yaml
Type: SwitchParameter
Parameter Sets: Session, Default, File
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
Parameter Sets: Default, File
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
Credentials used for the proxy server.

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

### -Scale
Scaling factor.

```yaml
Type: Single
Parameter Sets: Session, Default, File
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
Parameter Sets: Default, File
Aliases: None
Possible values: Custom, AuditProof, MailboxProof, LoginProtected, SinglePageApp, LowBandwidth, NetworkCapture, DownloadEvidence

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Selector
CSS selector to wait for before generating the PDF.

```yaml
Type: String
Parameter Sets: Session, Default, File
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
Parameter Sets: Session, Default, File
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
Parameter Sets: Default, File
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

### -UserDataDirectory
Persistent browser user-data directory used for URL or file captures.

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

### -Width
Paper width (e.g. 8.5in).

```yaml
Type: String
Parameter Sets: Session, Default, File
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

- `None`

## RELATED LINKS

- None
