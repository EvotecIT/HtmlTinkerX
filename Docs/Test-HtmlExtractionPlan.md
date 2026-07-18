---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Test-HtmlExtractionPlan
## SYNOPSIS
Recommends the most useful PSParseHTML extraction workflow for a page.

## SYNTAX
### Content (Default)
```powershell
Test-HtmlExtractionPlan [-Content] <string> [<CommonParameters>]
```

### Url
```powershell
Test-HtmlExtractionPlan [-Url] <uri> [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

### Path
```powershell
Test-HtmlExtractionPlan [-Path] <string> [<CommonParameters>]
```

## DESCRIPTION
Recommends the most useful PSParseHTML extraction workflow for a page.

## EXAMPLES

### EXAMPLE 1
```powershell
Test-HtmlExtractionPlan -Url https://example.com
```


## PARAMETERS

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
Proxy server address used when downloading Url.

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
Credentials used for the proxy server.

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

### -Url
URL of the page to inspect.

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

- `HtmlTinkerX.HtmlExtractionPlan`

## RELATED LINKS

- None
