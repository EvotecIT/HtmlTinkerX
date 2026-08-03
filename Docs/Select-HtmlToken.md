---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Select-HtmlToken
## SYNOPSIS
Selects likely CSRF, anti-forgery, nonce, and auth tokens from HTML.

## SYNTAX
### Content (Default)
```powershell
Select-HtmlToken -Content <string> [-Name <string[]>] [<CommonParameters>]
```

### File
```powershell
Select-HtmlToken -Path <string> [-Name <string[]>] [<CommonParameters>]
```

### Url
```powershell
Select-HtmlToken -Url <uri> [-Name <string[]>] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

## DESCRIPTION
Selects likely CSRF, anti-forgery, nonce, and auth tokens from HTML.

## EXAMPLES

### EXAMPLE 1
```powershell
Select-HtmlToken -Content 'Value'
```


### EXAMPLE 2
```powershell
Select-HtmlToken -Path 'C:\Path'
```


### EXAMPLE 3
```powershell
Select-HtmlToken -Url 'Value'
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
Accept wildcard characters: False
```

### -Name
Specific token field names to include. Defaults to common CSRF, XSRF, nonce, and auth token names.

```yaml
Type: String[]
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

- `HtmlTinkerX.HtmlToken`

## RELATED LINKS

- None
