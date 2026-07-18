---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Get-HtmlBrowserFormField
## SYNOPSIS
Cmdlet that returns all form input fields from HTML content or a URL.

## SYNTAX
### Content (Default)
```powershell
Get-HtmlBrowserFormField -Content <string> [<CommonParameters>]
```

### Url
```powershell
Get-HtmlBrowserFormField -Url <uri> [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

## DESCRIPTION
Cmdlet that returns all form input fields from HTML content or a URL.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-HtmlBrowserFormField -Content 'Value'
```


### EXAMPLE 2
```powershell
Get-HtmlBrowserFormField -Url 'Value'
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

### -Proxy
Proxy server address used when downloading.

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
Credentials for the proxy server.

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
URL of the page to download.

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

- `HtmlTinkerX.HtmlFormField`

## RELATED LINKS

- None
