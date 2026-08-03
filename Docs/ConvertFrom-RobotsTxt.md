---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# ConvertFrom-RobotsTxt
## SYNOPSIS
Parses robots.txt directives into structured rules.

## SYNTAX
### Content (Default)
```powershell
ConvertFrom-RobotsTxt -Content <string> [-BaseUrl <uri>] [<CommonParameters>]
```

### File
```powershell
ConvertFrom-RobotsTxt -Path <string> [-BaseUrl <uri>] [<CommonParameters>]
```

### Url
```powershell
ConvertFrom-RobotsTxt -Url <uri> [-BaseUrl <uri>] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

## DESCRIPTION
Parses robots.txt directives into structured rules.

## EXAMPLES

### EXAMPLE 1
```powershell
ConvertFrom-RobotsTxt -Content 'Value'
```


### EXAMPLE 2
```powershell
ConvertFrom-RobotsTxt -Path 'C:\Path'
```


### EXAMPLE 3
```powershell
ConvertFrom-RobotsTxt -Url 'Value'
```


## PARAMETERS

### -BaseUrl
Base URL used to resolve sitemap URLs. Defaults to Url when downloading.

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
Robots.txt content to parse.

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
Path to a robots.txt file.

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
URL of a robots.txt document to download and parse.

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

- `HtmlTinkerX.HtmlRobotsRule`

## RELATED LINKS

- None
