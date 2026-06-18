---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/PSParseHTML
schema: 2.0.0
---
# ConvertFrom-HtmlMeta
## SYNOPSIS
Parses <meta> tags from HTML content or a URL.

## SYNTAX
### Node (Default)
```powershell
ConvertFrom-HtmlMeta [-HtmlNode] <HtmlNode> [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

### Content
```powershell
ConvertFrom-HtmlMeta -Content <string> [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

### Url
```powershell
ConvertFrom-HtmlMeta -Url <uri> [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

## DESCRIPTION
Parses <meta> tags from HTML content or a URL.

## EXAMPLES

### EXAMPLE 1
```powershell
ConvertFrom-HtmlMeta -Url https://example.com
```


### EXAMPLE 2
```powershell
ConvertFrom-Html -Content $html | Select-HtmlNode -XPath '//head' | ConvertFrom-HtmlMeta
```


## PARAMETERS

### -Content
HTML content with meta tags.

```yaml
Type: String
Parameter Sets: Content
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue, ByPropertyName)
Accept wildcard characters: True
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
Accept wildcard characters: True
```

### -Proxy
Proxy server address to use when downloading by URL.

```yaml
Type: String
Parameter Sets: Node, Content, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -ProxyCredential
Credentials for the specified proxy server.

```yaml
Type: PSCredential
Parameter Sets: Node, Content, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Url
URL of a page with meta tags.

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `System.String
HtmlAgilityPack.HtmlNode`

## OUTPUTS

- `System.Management.Automation.PSObject`

## RELATED LINKS

- None
