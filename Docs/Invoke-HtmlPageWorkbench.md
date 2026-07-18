---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Invoke-HtmlPageWorkbench
## SYNOPSIS
Builds a one-page extraction workbench result with text, Markdown, data, forms, endpoints, and guidance.

## SYNTAX
### Content (Default)
```powershell
Invoke-HtmlPageWorkbench [-Content] <string> [-BaseUrl <uri>] [-IncludeLinkedScripts] [-IncludeExternalLinkedScripts] [-RenderedSnapshot <HtmlRenderedPageSnapshot>] [-NoStaticRenderedComparison] [-NoHtml] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

### Url
```powershell
Invoke-HtmlPageWorkbench [-Url] <uri> [-IncludeLinkedScripts] [-IncludeExternalLinkedScripts] [-RenderedSnapshot <HtmlRenderedPageSnapshot>] [-NoStaticRenderedComparison] [-NoHtml] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

### Path
```powershell
Invoke-HtmlPageWorkbench [-Path] <string> [-BaseUrl <uri>] [-IncludeLinkedScripts] [-IncludeExternalLinkedScripts] [-RenderedSnapshot <HtmlRenderedPageSnapshot>] [-NoStaticRenderedComparison] [-NoHtml] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

## DESCRIPTION
Builds a one-page extraction workbench result with text, Markdown, data, forms, endpoints, and guidance.

## EXAMPLES

### EXAMPLE 1
```powershell
Invoke-HtmlPageWorkbench -Url https://example.com
```


## PARAMETERS

### -BaseUrl
Base URL used to resolve relative links and assets. Defaults to Url when downloading.

```yaml
Type: Uri
Parameter Sets: Content, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Content
HTML content to inspect.

```yaml
Type: String
Parameter Sets: Content
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: True
```

### -IncludeExternalLinkedScripts
Allows linked-script endpoint inspection to download cross-origin scripts.

```yaml
Type: SwitchParameter
Parameter Sets: Content, Url, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -IncludeLinkedScripts
Downloads same-origin linked JavaScript files and inspects them for endpoints.

```yaml
Type: SwitchParameter
Parameter Sets: Content, Url, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -NoHtml
Omits the original HTML from the workbench result.

```yaml
Type: SwitchParameter
Parameter Sets: Content, Url, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -NoStaticRenderedComparison
Skips static-vs-rendered comparison when a rendered snapshot is supplied.

```yaml
Type: SwitchParameter
Parameter Sets: Content, Url, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Path
Path to a local HTML file to inspect.

```yaml
Type: String
Parameter Sets: Path
Aliases: File
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Proxy
Proxy server address used when downloading by URL or inspecting linked scripts.

```yaml
Type: String
Parameter Sets: Content, Url, Path
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
Parameter Sets: Content, Url, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -RenderedSnapshot
Rendered snapshot from Invoke-HtmlRendering -Snapshot used to enrich the workbench.

```yaml
Type: HtmlRenderedPageSnapshot
Parameter Sets: Content, Url, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Url
URL of the page to download and inspect.

```yaml
Type: Uri
Parameter Sets: Url
Aliases: Uri
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `System.String`

## OUTPUTS

- `HtmlTinkerX.HtmlPageWorkbenchResult`

## RELATED LINKS

- None
