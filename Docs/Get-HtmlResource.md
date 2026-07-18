---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Get-HtmlResource
## SYNOPSIS
Parses external and inline resources and returns their links or downloads them.

## SYNTAX
### Content (Default)
```powershell
Get-HtmlResource -Content <string> [-OutDirectory <string>] [-Download] [-IncludeCss] [-IncludeInline] [-AsContent] [<CommonParameters>]
```

### File
```powershell
Get-HtmlResource -Path <string> [-OutDirectory <string>] [-Download] [-IncludeCss] [-IncludeInline] [-AsContent] [<CommonParameters>]
```

### Url
```powershell
Get-HtmlResource -Url <uri> [-Proxy <string>] [-ProxyCredential <pscredential>] [-OutDirectory <string>] [-Download] [-IncludeCss] [-IncludeInline] [-AsContent] [<CommonParameters>]
```

## DESCRIPTION
Parses external and inline resources and returns their links or downloads them.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-HtmlResource -Content 'Value'
```


### EXAMPLE 2
```powershell
Get-HtmlResource -Path 'C:\Path'
```


### EXAMPLE 3
```powershell
Get-HtmlResource -Url 'Value'
```


## PARAMETERS

### -AsContent
Return the content of external resources.

```yaml
Type: SwitchParameter
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

### -Download
Download the scripts instead of returning URLs. Only valid with -Url.

```yaml
Type: SwitchParameter
Parameter Sets: Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -IncludeCss
Include CSS <link> and <style> tags.

```yaml
Type: SwitchParameter
Parameter Sets: Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -IncludeInline
Include inline <script> or <style> content.

```yaml
Type: SwitchParameter
Parameter Sets: Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -OutDirectory
Directory where scripts will be saved when Download is specified.

```yaml
Type: String
Parameter Sets: Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
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
Credentials for the specified proxy server.

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
URL of an HTML page.

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

- `HtmlTinkerX.HtmlResourceLink`

## RELATED LINKS

- None
