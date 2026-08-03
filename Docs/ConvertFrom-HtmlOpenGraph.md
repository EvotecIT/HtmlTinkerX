---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# ConvertFrom-HtmlOpenGraph
## SYNOPSIS
Extracts Open Graph metadata from HTML content or a URL.

## SYNTAX
### Node (Default)
```powershell
ConvertFrom-HtmlOpenGraph [-HtmlNode] <HtmlNode> [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

### Content
```powershell
ConvertFrom-HtmlOpenGraph -Content <string> [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

### Url
```powershell
ConvertFrom-HtmlOpenGraph -Url <uri> [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

## DESCRIPTION
Extracts Open Graph metadata from HTML content or a URL.

## EXAMPLES

### EXAMPLE 1
```powershell
ConvertFrom-HtmlOpenGraph -Content $html
```


### EXAMPLE 2
```powershell
ConvertFrom-Html -Content $html | Select-HtmlNode -XPath '//head' | ConvertFrom-HtmlOpenGraph
```


## PARAMETERS

### -Content
HTML markup containing Open Graph meta tags.

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

### -HtmlNode
HtmlAgilityPack node to inspect.

```yaml
Type: HtmlNode
Parameter Sets: Node
Aliases: Node, InputObject
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Proxy
Proxy server address when downloading by URL.

```yaml
Type: String
Parameter Sets: Node, Content, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ProxyCredential
Credentials for the proxy server.

```yaml
Type: PSCredential
Parameter Sets: Node, Content, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Url
URL of a page with Open Graph metadata.

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `System.String`
- `HtmlAgilityPack.HtmlNode`

## OUTPUTS

- `System.Management.Automation.PSObject`

## RELATED LINKS

- None
