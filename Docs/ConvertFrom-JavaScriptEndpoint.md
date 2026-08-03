---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# ConvertFrom-JavaScriptEndpoint
## SYNOPSIS
Discovers likely endpoints from static JavaScript source or inline HTML scripts.

## SYNTAX
### Content (Default)
```powershell
ConvertFrom-JavaScriptEndpoint -Content <string> [-Html] [<CommonParameters>]
```

### File
```powershell
ConvertFrom-JavaScriptEndpoint -Path <string> [-Html] [<CommonParameters>]
```

### Url
```powershell
ConvertFrom-JavaScriptEndpoint -Url <uri> [-Html] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

## DESCRIPTION
Discovers likely endpoints from static JavaScript source or inline HTML scripts.

## EXAMPLES

### EXAMPLE 1
```powershell
ConvertFrom-JavaScriptEndpoint -Content 'Value'
```


### EXAMPLE 2
```powershell
ConvertFrom-JavaScriptEndpoint -Path 'C:\Path'
```


### EXAMPLE 3
```powershell
ConvertFrom-JavaScriptEndpoint -Url 'Value'
```


## PARAMETERS

### -Content
JavaScript or HTML content to inspect.

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

### -Html
Treat the input as HTML and inspect inline script content.

```yaml
Type: SwitchParameter
Parameter Sets: Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
Path to a JavaScript or HTML file.

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
URL of a JavaScript or HTML document to download and inspect.

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

## OUTPUTS

- `HtmlTinkerX.HtmlJavaScriptEndpoint`

## RELATED LINKS

- None
