---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/PSParseHTML
schema: 2.0.0
---
# ConvertFrom-HtmlScriptData
## SYNOPSIS
Extracts generic JSON data embedded in script tags.

## SYNTAX
### Node (Default)
```powershell
ConvertFrom-HtmlScriptData [-HtmlNode] <HtmlNode> [<CommonParameters>]
```

### Content
```powershell
ConvertFrom-HtmlScriptData -Content <string> [<CommonParameters>]
```

### File
```powershell
ConvertFrom-HtmlScriptData -Path <string> [<CommonParameters>]
```

### Url
```powershell
ConvertFrom-HtmlScriptData -Url <uri> [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

## DESCRIPTION
Extracts generic JSON data embedded in script tags.

## EXAMPLES

### EXAMPLE 1
```powershell
ConvertFrom-HtmlScriptData -Content $html
```


### EXAMPLE 2
```powershell
ConvertFrom-Html -Content $html | Select-HtmlNode -XPath '//script[@type="application/json"]' | ConvertFrom-HtmlScriptData
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

- `HtmlTinkerX.HtmlScriptDataItem`

## RELATED LINKS

- None
