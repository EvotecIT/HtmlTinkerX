---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/PSParseHTML
schema: 2.0.0
---
# Select-HtmlData
## SYNOPSIS
Selects normalized structured data, links, assets, tokens, forms, and app state from HTML.

## SYNTAX
### Node (Default)
```powershell
Select-HtmlData [-HtmlNode] <Object> [-Kind <string[]>] [-BaseUrl <uri>] [<CommonParameters>]
```

### Content
```powershell
Select-HtmlData -Content <string> [-Kind <string[]>] [-BaseUrl <uri>] [<CommonParameters>]
```

### File
```powershell
Select-HtmlData -Path <string> [-Kind <string[]>] [-BaseUrl <uri>] [<CommonParameters>]
```

### Url
```powershell
Select-HtmlData -Url <uri> [-Kind <string[]>] [-BaseUrl <uri>] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

## DESCRIPTION
Selects normalized structured data, links, assets, tokens, forms, and app state from HTML.

## EXAMPLES

### EXAMPLE 1
```powershell
Select-HtmlData -Url https://example.org -BaseUrl https://example.org
```


### EXAMPLE 2
```powershell
Select-HtmlData -Content $html -Kind JsonLd,OpenGraph,Meta,Microdata
```


### EXAMPLE 3
```powershell
Select-HtmlNode -Content $html -XPath '//head' | Select-HtmlData -Kind HeadLink,Meta
```


## PARAMETERS

### -BaseUrl
Base URL used to resolve relative links and assets. Defaults to Url when downloading.

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
Accept wildcard characters: True
```

### -Kind
Data families to include. Supported values include JsonLd, Microdata, OpenGraph, Meta, HeadLink, AppState, ScriptData, Token, Form, Link, and Asset.

```yaml
Type: String[]
Parameter Sets: Node, Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
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
System.Object`

## OUTPUTS

- `HtmlTinkerX.HtmlDataItem`

## RELATED LINKS

- None
