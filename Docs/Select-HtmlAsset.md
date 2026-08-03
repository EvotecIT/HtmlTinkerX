---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Select-HtmlAsset
## SYNOPSIS
Extracts scripts, stylesheets, images, preloads, manifests, and icons from HTML.

## SYNTAX
### Content (Default)
```powershell
Select-HtmlAsset -Content <string> [-BaseUrl <uri>] [-IncludeInline] [<CommonParameters>]
```

### File
```powershell
Select-HtmlAsset -Path <string> [-BaseUrl <uri>] [-IncludeInline] [<CommonParameters>]
```

### Url
```powershell
Select-HtmlAsset -Url <uri> [-BaseUrl <uri>] [-IncludeInline] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

## DESCRIPTION
Extracts scripts, stylesheets, images, preloads, manifests, and icons from HTML.

## EXAMPLES

### EXAMPLE 1
```powershell
Select-HtmlAsset -Content '<link rel="manifest" href="/site.webmanifest"><img src="/logo.png">' -BaseUrl 'https://example.org/'
```


## PARAMETERS

### -BaseUrl
Base URL used to resolve relative asset URLs. Defaults to Url when downloading.

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

### -IncludeInline
Includes inline script and style blocks as assets.

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

- `HtmlTinkerX.HtmlAssetReference`

## RELATED LINKS

- None
