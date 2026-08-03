---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# ConvertFrom-WebManifest
## SYNOPSIS
Parses a web app manifest JSON document.

## SYNTAX
### Content (Default)
```powershell
ConvertFrom-WebManifest -Content <string> [-BaseUrl <uri>] [<CommonParameters>]
```

### File
```powershell
ConvertFrom-WebManifest -Path <string> [-BaseUrl <uri>] [<CommonParameters>]
```

### Url
```powershell
ConvertFrom-WebManifest -Url <uri> [-BaseUrl <uri>] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

## DESCRIPTION
Parses a web app manifest JSON document.

## EXAMPLES

### EXAMPLE 1
```powershell
ConvertFrom-WebManifest -Content 'Value'
```


### EXAMPLE 2
```powershell
ConvertFrom-WebManifest -Path 'C:\Path'
```


### EXAMPLE 3
```powershell
ConvertFrom-WebManifest -Url 'Value'
```


## PARAMETERS

### -BaseUrl
Base URL used to resolve manifest URLs. Defaults to Url when downloading.

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
Web manifest JSON content.

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
Path to a web manifest JSON file.

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
URL of a web manifest JSON file to download and parse.

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

- `HtmlTinkerX.HtmlWebManifestDocument`

## RELATED LINKS

- None
