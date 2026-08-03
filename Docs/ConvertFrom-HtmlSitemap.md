---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# ConvertFrom-HtmlSitemap
## SYNOPSIS
Extracts URLs from sitemap urlset or sitemap index XML.

## SYNTAX
### Content (Default)
```powershell
ConvertFrom-HtmlSitemap -Content <string> [-BaseUrl <uri>] [<CommonParameters>]
```

### File
```powershell
ConvertFrom-HtmlSitemap -Path <string> [-BaseUrl <uri>] [<CommonParameters>]
```

### Url
```powershell
ConvertFrom-HtmlSitemap -Url <uri> [-BaseUrl <uri>] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

## DESCRIPTION
Extracts URLs from sitemap urlset or sitemap index XML.

## EXAMPLES

### EXAMPLE 1
```powershell
ConvertFrom-HtmlSitemap -Content 'Value'
```


### EXAMPLE 2
```powershell
ConvertFrom-HtmlSitemap -Path 'C:\Path'
```


### EXAMPLE 3
```powershell
ConvertFrom-HtmlSitemap -Url 'Value'
```


## PARAMETERS

### -BaseUrl
Base URL used to resolve relative sitemap locations. Defaults to Url when downloading.

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
Sitemap XML content.

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

### -Path
Path to a sitemap XML file.

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
URL of a sitemap XML document.

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

- `System.String`

## RELATED LINKS

- None
