---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# ConvertFrom-HtmlImageCandidate
## SYNOPSIS
Extracts image URLs and responsive srcset candidates from HTML.

## SYNTAX
### Content (Default)
```powershell
ConvertFrom-HtmlImageCandidate -Content <string> [-BaseUrl <uri>] [<CommonParameters>]
```

### File
```powershell
ConvertFrom-HtmlImageCandidate -Path <string> [-BaseUrl <uri>] [<CommonParameters>]
```

### Url
```powershell
ConvertFrom-HtmlImageCandidate -Url <uri> [-BaseUrl <uri>] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

## DESCRIPTION
Extracts image URLs and responsive srcset candidates from HTML.

## EXAMPLES

### EXAMPLE 1
```powershell
ConvertFrom-HtmlImageCandidate -Content 'Value'
```


### EXAMPLE 2
```powershell
ConvertFrom-HtmlImageCandidate -Path 'C:\Path'
```


### EXAMPLE 3
```powershell
ConvertFrom-HtmlImageCandidate -Url 'Value'
```


## PARAMETERS

### -BaseUrl
Base URL used to resolve relative image URLs. Defaults to Url when downloading.

```yaml
Type: Uri
Parameter Sets: Content, File, Url
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

- `System.String`

## OUTPUTS

- `HtmlTinkerX.HtmlImageCandidate`

## RELATED LINKS

- None
