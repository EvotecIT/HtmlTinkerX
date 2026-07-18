---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# ConvertFrom-Html
## SYNOPSIS
Parses HTML content from a string or a remote page.

## SYNTAX
### Content (Default)
```powershell
ConvertFrom-Html -Content <string> [-Engine <HtmlParserEngine>] [-Proxy <string>] [-ProxyCredential <pscredential>] [-Raw] [<CommonParameters>]
```

### Url
```powershell
ConvertFrom-Html -Url <uri> [-Engine <HtmlParserEngine>] [-Proxy <string>] [-ProxyCredential <pscredential>] [-Raw] [<CommonParameters>]
```

## DESCRIPTION
The cmdlet can read raw HTML or download a web page specified with
-Url. When downloading, optional -Proxy and
-ProxyCredential parameters control the web request.

## EXAMPLES

### EXAMPLE 1
```powershell
ConvertFrom-Html -Url https://example.com
```


### EXAMPLE 2
```powershell
ConvertFrom-Html -Url https://example.com -Proxy http://proxy:8080
```


## PARAMETERS

### -Content
HTML content to parse.

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

### -Engine
Selects parsing engine.

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

### -Proxy
Optional proxy server address used when fetching content from Url.
Include the protocol and port number if required.

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
Credentials used to authenticate against the Proxy server.

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

### -Raw
Return raw document object.

```yaml
Type: SwitchParameter
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
URL of a HTML page.

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

- `System.String`

## OUTPUTS

- `System.Object`

## RELATED LINKS

- None
