---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# ConvertFrom-WellKnownText
## SYNOPSIS
Parses common well-known text files such as security.txt, humans.txt, and ads.txt.

## SYNTAX
### Content (Default)
```powershell
ConvertFrom-WellKnownText -Content <string> -Kind <string> [-BaseUrl <uri>] [<CommonParameters>]
```

### File
```powershell
ConvertFrom-WellKnownText -Path <string> -Kind <string> [-BaseUrl <uri>] [<CommonParameters>]
```

### Url
```powershell
ConvertFrom-WellKnownText -Url <uri> -Kind <string> [-BaseUrl <uri>] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

## DESCRIPTION
Parses common well-known text files such as security.txt, humans.txt, and ads.txt.

## EXAMPLES

### EXAMPLE 1
```powershell
ConvertFrom-WellKnownText -Content 'Value' -Kind 'Value'
```


### EXAMPLE 2
```powershell
ConvertFrom-WellKnownText -Path 'C:\Path' -Kind 'Value'
```


### EXAMPLE 3
```powershell
ConvertFrom-WellKnownText -Url 'Value' -Kind 'Value'
```


## PARAMETERS

### -BaseUrl
Base URL used to resolve relative security.txt URLs. Defaults to Url when downloading.

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
Well-known text file content.

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

### -Kind
Kind of well-known text file to parse.

```yaml
Type: String
Parameter Sets: Content, File, Url
Aliases: None
Possible values: SecurityTxt, HumansTxt, AdsTxt, security.txt, humans.txt, ads.txt

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
Path to a well-known text file.

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
URL of a well-known text file to download and parse.

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

- `HtmlTinkerX.HtmlWellKnownRecord`

## RELATED LINKS

- None
