---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# ConvertTo-HtmlDatasetJsonL
## SYNOPSIS
Converts a page workbench result or HTML input into LLM-ready dataset JSON Lines.

## SYNTAX
### Workbench (Default)
```powershell
ConvertTo-HtmlDatasetJsonL [-Workbench] <HtmlPageWorkbenchResult> [-MaxChunkWords <int>] [-NoMarkdown] [-NoProvenance] [-NoRedactionHints] [-AsObject] [<CommonParameters>]
```

### Content
```powershell
ConvertTo-HtmlDatasetJsonL [-Content] <string> [-BaseUrl <uri>] [-MaxChunkWords <int>] [-NoMarkdown] [-NoProvenance] [-NoRedactionHints] [-AsObject] [<CommonParameters>]
```

### Url
```powershell
ConvertTo-HtmlDatasetJsonL [-Url] <uri> [-MaxChunkWords <int>] [-NoMarkdown] [-NoProvenance] [-NoRedactionHints] [-AsObject] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

### Path
```powershell
ConvertTo-HtmlDatasetJsonL [-Path] <string> [-BaseUrl <uri>] [-MaxChunkWords <int>] [-NoMarkdown] [-NoProvenance] [-NoRedactionHints] [-AsObject] [<CommonParameters>]
```

## DESCRIPTION
Converts a page workbench result or HTML input into LLM-ready dataset JSON Lines.

## EXAMPLES

### EXAMPLE 1
```powershell
Invoke-HtmlPageWorkbench -Url https://example.com | ConvertTo-HtmlDatasetJsonL
```


## PARAMETERS

### -AsObject
Returns chunk objects instead of JSON Lines.

```yaml
Type: SwitchParameter
Parameter Sets: Workbench, Content, Url, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -BaseUrl
Base URL used to resolve relative links and provenance when Content or Path is used.

```yaml
Type: Uri
Parameter Sets: Content, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Content
HTML content to inspect and convert.

```yaml
Type: String
Parameter Sets: Content
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -MaxChunkWords
Maximum number of words per dataset chunk.

```yaml
Type: Int32
Parameter Sets: Workbench, Content, Url, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -NoMarkdown
Omits markdown content from dataset chunks.

```yaml
Type: SwitchParameter
Parameter Sets: Workbench, Content, Url, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -NoProvenance
Omits provenance entries from dataset chunks.

```yaml
Type: SwitchParameter
Parameter Sets: Workbench, Content, Url, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -NoRedactionHints
Omits redaction hints from dataset chunks.

```yaml
Type: SwitchParameter
Parameter Sets: Workbench, Content, Url, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
Path to a local HTML file to convert.

```yaml
Type: String
Parameter Sets: Path
Aliases: File
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Proxy
Proxy server address used when downloading by URL.

```yaml
Type: String
Parameter Sets: Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
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
Accept wildcard characters: False
```

### -Url
URL of the page to download and convert.

```yaml
Type: Uri
Parameter Sets: Url
Aliases: Uri
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Workbench
Page workbench result to convert.

```yaml
Type: HtmlPageWorkbenchResult
Parameter Sets: Workbench
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `HtmlTinkerX.HtmlPageWorkbenchResult`
- `System.String`

## OUTPUTS

- `System.String`
- `HtmlTinkerX.HtmlPageDatasetChunk`

## RELATED LINKS

- None
