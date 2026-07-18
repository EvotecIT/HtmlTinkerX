---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Compare-HtmlStaticRendered
## SYNOPSIS
Compares static HTML with browser-rendered HTML using parsing-friendly signatures.

## SYNTAX
### Content (Default)
```powershell
Compare-HtmlStaticRendered -StaticContent <string> -RenderedContent <string> [-BaseUrl <uri>] [<CommonParameters>]
```

### File
```powershell
Compare-HtmlStaticRendered -StaticPath <string> -RenderedPath <string> [-BaseUrl <uri>] [<CommonParameters>]
```

### Url
```powershell
Compare-HtmlStaticRendered -Url <uri> [-BaseUrl <uri>] [-Browser <HtmlBrowserEngine>] [-Clean] [-Visible] [-Timeout <int>] [-Proxy <string>] [-ProxyCredential <pscredential>] [-Credential <pscredential>] [-Username <string>] [-Password <string>] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

## DESCRIPTION
Compares static HTML with browser-rendered HTML using parsing-friendly signatures.

## EXAMPLES

### EXAMPLE 1
```powershell
Compare-HtmlStaticRendered -StaticContent $staticHtml -RenderedContent $renderedHtml
```


### EXAMPLE 2
```powershell
Compare-HtmlStaticRendered -StaticPath .\static.html -RenderedPath .\rendered.html
```


### EXAMPLE 3
```powershell
Compare-HtmlStaticRendered -Url https://example.org/app -Browser Chromium -Timeout 15000
```


## PARAMETERS

### -BaseUrl
Base URL used to resolve relative links in comparison signatures. Defaults to Url when downloading.

```yaml
Type: Uri
Parameter Sets: Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Browser
Browser engine used for rendered URL comparisons.

```yaml
Type: HtmlBrowserEngine
Parameter Sets: Url
Aliases: None
Possible values: Chromium, Firefox, WebKit

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -CancellationToken
Token used to cancel the browser-rendering operation.

```yaml
Type: CancellationToken
Parameter Sets: Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Clean
Force re-download of browser runtimes before rendering.

```yaml
Type: SwitchParameter
Parameter Sets: Url
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
Parameter Sets: Url
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
Parameter Sets: Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Proxy
Proxy server address used when downloading and rendering by URL.

```yaml
Type: String
Parameter Sets: Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -ProxyCredential
Credentials used with the proxy server.

```yaml
Type: PSCredential
Parameter Sets: Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -RenderedContent
Rendered HTML content after browser execution.

```yaml
Type: String
Parameter Sets: Content
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByPropertyName)
Accept wildcard characters: True
```

### -RenderedPath
Path to the browser-rendered HTML file.

```yaml
Type: String
Parameter Sets: File
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -StaticContent
Original static HTML content before browser execution.

```yaml
Type: String
Parameter Sets: Content
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByPropertyName)
Accept wildcard characters: True
```

### -StaticPath
Path to the original static HTML file.

```yaml
Type: String
Parameter Sets: File
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Timeout
Timeout in milliseconds for browser rendering operations.

```yaml
Type: Int32
Parameter Sets: Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Url
URL of a page to download statically and render with a browser before comparing.

```yaml
Type: Uri
Parameter Sets: Url
Aliases: Uri
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Username
Username for pages secured with basic authentication.

```yaml
Type: String
Parameter Sets: Url
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
Parameter Sets: Url
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

- `System.String`

## OUTPUTS

- `HtmlTinkerX.HtmlStaticRenderedComparison`

## RELATED LINKS

- None
