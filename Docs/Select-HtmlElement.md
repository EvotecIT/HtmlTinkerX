---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Select-HtmlElement
## SYNOPSIS
Selects elements from static HTML with a CSS selector.

## SYNTAX
### Input (Default)
```powershell
Select-HtmlElement [-Input] <Object> [-Selector] <string> [-First] [-Required] [<CommonParameters>]
```

### Content
```powershell
Select-HtmlElement [-Selector] <string> -Content <string> [-First] [-Required] [<CommonParameters>]
```

### File
```powershell
Select-HtmlElement [-Selector] <string> -Path <string> [-First] [-Required] [<CommonParameters>]
```

### Url
```powershell
Select-HtmlElement [-Selector] <string> -Url <uri> [-First] [-Required] [-Proxy <string>] [-ProxyCredential <pscredential>] [-UserAgent <string>] [-Header <hashtable>] [<CommonParameters>]
```

## DESCRIPTION
Selects elements from static HTML with a CSS selector.

## EXAMPLES

### EXAMPLE 1
```powershell
ConvertFrom-Html -Content $html | Select-HtmlElement -Selector '.product-card'
```


### EXAMPLE 2
```powershell
Select-HtmlElement -Url https://example.org -Selector 'h1' -First
```


## PARAMETERS

### -Content
HTML content to search.

```yaml
Type: String
Parameter Sets: Content
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -First
Return only the first matching element.

```yaml
Type: SwitchParameter
Parameter Sets: Input, Content, File, Url
Aliases: Single
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Header
Additional or replacement HTTP headers used when downloading Url.

```yaml
Type: Hashtable
Parameter Sets: Url
Aliases: Headers
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Input
Parsed document, element, HtmlAgilityPack node, or raw markup to search.

```yaml
Type: Object
Parameter Sets: Input
Aliases: HtmlDocument, HtmlNode, Node, InputObject
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Path
Path to an HTML file to search.

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

### -Required
Throw when the selector matches no elements.

```yaml
Type: SwitchParameter
Parameter Sets: Input, Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Selector
CSS selector evaluated against the static document or input element.

```yaml
Type: String
Parameter Sets: Input, Content, File, Url
Aliases: None
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Url
URL of an HTML page to download and search.

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

### -UserAgent
User-Agent header used when downloading Url.

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `System.Object`

## OUTPUTS

- `AngleSharp.Dom.IElement`

## RELATED LINKS

- None
