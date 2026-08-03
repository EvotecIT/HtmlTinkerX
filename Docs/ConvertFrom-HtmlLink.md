---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# ConvertFrom-HtmlLink
## SYNOPSIS
Extracts anchor links from HTML together with text, title, and nearby context.

## SYNTAX
### Content (Default)
```powershell
ConvertFrom-HtmlLink -Content <string> [-BaseUrl <uri>] [-MaxContextLength <int>] [<CommonParameters>]
```

### File
```powershell
ConvertFrom-HtmlLink -Path <string> [-BaseUrl <uri>] [-MaxContextLength <int>] [<CommonParameters>]
```

### Url
```powershell
ConvertFrom-HtmlLink -Url <uri> [-BaseUrl <uri>] [-MaxContextLength <int>] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

## DESCRIPTION
Extracts anchor links from HTML together with text, title, and nearby context.

## EXAMPLES

### EXAMPLE 1
```powershell
ConvertFrom-HtmlLink -Content 'Value'
```


### EXAMPLE 2
```powershell
ConvertFrom-HtmlLink -Path 'C:\Path'
```


### EXAMPLE 3
```powershell
ConvertFrom-HtmlLink -Url 'Value'
```


## PARAMETERS

### -BaseUrl
Base URL used to resolve relative links. Defaults to Url when downloading.

```yaml
Type: Uri
Parameter Sets: Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
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
Accept wildcard characters: False
```

### -MaxContextLength
Maximum length of the nearby context text returned for each link.

```yaml
Type: Int32
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
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `System.String`

## OUTPUTS

- `HtmlTinkerX.HtmlDiscoveredLink`

## RELATED LINKS

- None
