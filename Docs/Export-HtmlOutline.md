---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Export-HtmlOutline
## SYNOPSIS
Exports a hierarchical outline of headings in HTML content to a JSON file.

## SYNTAX
### Content (Default)
```powershell
Export-HtmlOutline [-Content] <string> [-Path] <string> [-Engine <HtmlParserEngine>] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

### Url
```powershell
Export-HtmlOutline [-Url] <uri> [-Path] <string> [-Engine <HtmlParserEngine>] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

## DESCRIPTION
Exports a hierarchical outline of headings in HTML content to a JSON file.

## EXAMPLES

### EXAMPLE 1
```powershell
Export-HTMLOutline -Url https://example.com -Path outline.json
```


## PARAMETERS

### -Content
HTML markup to analyze.

```yaml
Type: String
Parameter Sets: Content
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue, ByPropertyName)
Accept wildcard characters: True
```

### -Engine
Parsing engine used for processing HTML.

```yaml
Type: HtmlParserEngine
Parameter Sets: Content, Url
Aliases: None
Possible values: AngleSharp, AgilityPack

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Path
Destination path for the JSON outline.

```yaml
Type: String
Parameter Sets: Content, Url
Aliases: None
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Proxy
Proxy server address used when Url is specified.

```yaml
Type: String
Parameter Sets: Content, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -ProxyCredential
Credentials used with the specified Proxy.

```yaml
Type: PSCredential
Parameter Sets: Content, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Url
URL of the page to analyze.

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

- `System.Object`

## RELATED LINKS

- None
