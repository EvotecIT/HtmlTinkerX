---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Select-HtmlStyleUsage
## SYNOPSIS
Reports whether CSS selectors from inline or supplied CSS match elements in an HTML document.

## SYNTAX
### Node (Default)
```powershell
Select-HtmlStyleUsage [-HtmlNode] <Object> [-CssContent <string>] [-CssPath <string>] [-UsedOnly] [-MaxMatchedElements <int>] [<CommonParameters>]
```

### Content
```powershell
Select-HtmlStyleUsage -Content <string> [-CssContent <string>] [-CssPath <string>] [-UsedOnly] [-MaxMatchedElements <int>] [<CommonParameters>]
```

### File
```powershell
Select-HtmlStyleUsage -Path <string> [-CssContent <string>] [-CssPath <string>] [-UsedOnly] [-MaxMatchedElements <int>] [<CommonParameters>]
```

### Url
```powershell
Select-HtmlStyleUsage -Url <uri> [-CssContent <string>] [-CssPath <string>] [-UsedOnly] [-MaxMatchedElements <int>] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

## DESCRIPTION
Reports whether CSS selectors from inline or supplied CSS match elements in an HTML document.

## EXAMPLES

### EXAMPLE 1
```powershell
Select-HtmlStyleUsage -Content $html | Where-Object IsUsed -EQ $false
```


### EXAMPLE 2
```powershell
Select-HtmlStyleUsage -Content $html -CssPath .\site.css -UsedOnly
```


### EXAMPLE 3
```powershell
Select-HtmlNode -Content $html -CssSelector 'main' | Select-HtmlStyleUsage -MaxMatchedElements 3
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
Position: named
Default value: None
Accept pipeline input: True (ByValue, ByPropertyName)
Accept wildcard characters: False
```

### -CssContent
CSS content to compare against the HTML. When omitted, inline style elements are used.

```yaml
Type: String
Parameter Sets: Node, Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -CssPath
Path to a CSS file to compare against the HTML.

```yaml
Type: String
Parameter Sets: Node, Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -HtmlNode
HtmlAgilityPack node or document to inspect.

```yaml
Type: Object
Parameter Sets: Node
Aliases: Node, InputObject
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -MaxMatchedElements
Maximum representative matched element selectors returned for each CSS rule.

```yaml
Type: Int32
Parameter Sets: Node, Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
Path to an HTML file.

```yaml
Type: String
Parameter Sets: File
Aliases: File
Possible values:

Required: True
Position: named
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
URL of an HTML page to download and inspect.

```yaml
Type: Uri
Parameter Sets: Url
Aliases: Uri
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -UsedOnly
Returns only selectors that matched at least one element or had a selector error.

```yaml
Type: SwitchParameter
Parameter Sets: Node, Content, File, Url
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

- `System.String`
- `System.Object`

## OUTPUTS

- `HtmlTinkerX.HtmlStyleUsageItem`

## RELATED LINKS

- None
