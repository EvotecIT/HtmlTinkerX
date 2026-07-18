---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# ConvertFrom-HtmlMicrodata
## SYNOPSIS
Extracts microdata items from HTML content or a URL.

## SYNTAX
### Node (Default)
```powershell
ConvertFrom-HtmlMicrodata [-HtmlNode] <HtmlNode> [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

### Content
```powershell
ConvertFrom-HtmlMicrodata -Content <string> [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

### Url
```powershell
ConvertFrom-HtmlMicrodata -Url <uri> [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

## DESCRIPTION
Extracts microdata items from HTML content or a URL.

## EXAMPLES

### EXAMPLE 1
```powershell
ConvertFrom-HtmlMicrodata -Content $html
```


### EXAMPLE 2
```powershell
ConvertFrom-Html -Content $html | Select-HtmlNode -XPath '//*[@itemscope]' | ConvertFrom-HtmlMicrodata
```


## PARAMETERS

### -Content
HTML markup containing microdata.

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
Accept wildcard characters: True
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
Accept wildcard characters: True
```

### -Url
URL of a page with microdata.

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
