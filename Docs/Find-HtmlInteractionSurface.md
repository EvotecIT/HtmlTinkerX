---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/PSParseHTML
schema: 2.0.0
---
# Find-HtmlInteractionSurface
## SYNOPSIS
Finds forms, hidden fields, tokens, inline endpoints, and optional linked-script endpoints in HTML.

## SYNTAX
### Node (Default)
```powershell
Find-HtmlInteractionSurface [-HtmlNode] <Object> [-BaseUrl <uri>] [-IncludeLinkedScripts] [-IncludeExternalLinkedScripts] [<CommonParameters>]
```

### Content
```powershell
Find-HtmlInteractionSurface -Content <string> [-BaseUrl <uri>] [-IncludeLinkedScripts] [-IncludeExternalLinkedScripts] [<CommonParameters>]
```

### File
```powershell
Find-HtmlInteractionSurface -Path <string> [-BaseUrl <uri>] [-IncludeLinkedScripts] [-IncludeExternalLinkedScripts] [<CommonParameters>]
```

### Url
```powershell
Find-HtmlInteractionSurface -Url <uri> [-BaseUrl <uri>] [-IncludeLinkedScripts] [-IncludeExternalLinkedScripts] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

## DESCRIPTION
Finds forms, hidden fields, tokens, inline endpoints, and optional linked-script endpoints in HTML.

## EXAMPLES

### EXAMPLE 1
```powershell
Find-HtmlInteractionSurface -Content $html
```


### EXAMPLE 2
```powershell
Find-HtmlInteractionSurface -Url https://example.org/app -IncludeLinkedScripts
```


### EXAMPLE 3
```powershell
Select-HtmlNode -Content $html -CssSelector 'form' | Find-HtmlInteractionSurface
```


## PARAMETERS

### -BaseUrl
Base URL used to resolve linked script URLs. Defaults to Url when downloading, and can be supplied by an absolute document base element.

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

### -IncludeExternalLinkedScripts
Allows cross-origin linked JavaScript downloads when IncludeLinkedScripts is used.

```yaml
Type: SwitchParameter
Parameter Sets: Node, Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -IncludeLinkedScripts
Downloads and inspects same-origin linked JavaScript files when BaseUrl, Url, or an absolute document base element is available.

```yaml
Type: SwitchParameter
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

- `HtmlTinkerX.HtmlInteractionSurfaceItem`

## RELATED LINKS

- None
