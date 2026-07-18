---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# ConvertFrom-HtmlHeadLink
## SYNOPSIS
Extracts canonical, alternate, feed, icon, manifest, preload, and related head links.

## SYNTAX
### Node (Default)
```powershell
ConvertFrom-HtmlHeadLink [-HtmlNode] <HtmlNode> [-BaseUrl <uri>] [<CommonParameters>]
```

### Content
```powershell
ConvertFrom-HtmlHeadLink -Content <string> [-BaseUrl <uri>] [<CommonParameters>]
```

### File
```powershell
ConvertFrom-HtmlHeadLink -Path <string> [-BaseUrl <uri>] [<CommonParameters>]
```

### Url
```powershell
ConvertFrom-HtmlHeadLink -Url <uri> [-BaseUrl <uri>] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

## DESCRIPTION
Extracts canonical, alternate, feed, icon, manifest, preload, and related head links.

## EXAMPLES

### EXAMPLE 1
```powershell
ConvertFrom-HtmlHeadLink -Url https://example.org/page
```


### EXAMPLE 2
```powershell
ConvertFrom-Html -Content $html | Select-HtmlNode -XPath '//head' | ConvertFrom-HtmlHeadLink -BaseUrl https://example.org/page
```


## PARAMETERS

### -BaseUrl
Base URL used to resolve relative URLs. Defaults to Url when downloading.

```yaml
Type: Uri
Parameter Sets: Node, Content, File, Url
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
Accept wildcard characters: True
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
Accept wildcard characters: True
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `System.String
HtmlAgilityPack.HtmlNode`

## OUTPUTS

- `HtmlTinkerX.HtmlHeadLink`

## RELATED LINKS

- None
