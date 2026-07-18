---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# ConvertFrom-HtmlAttributes
## SYNOPSIS
Extracts HTML elements by tag, class, id or name attributes.

## SYNTAX
### Content (Default)
```powershell
ConvertFrom-HtmlAttributes -Content <string> [-Tag <string>] [-Class <string>] [-Id <string>] [-Name <string>] [-Proxy <string>] [-ProxyCredential <pscredential>] [-ReturnObject] [<CommonParameters>]
```

### File
```powershell
ConvertFrom-HtmlAttributes -Path <string> [-Tag <string>] [-Class <string>] [-Id <string>] [-Name <string>] [-Proxy <string>] [-ProxyCredential <pscredential>] [-ReturnObject] [<CommonParameters>]
```

### Url
```powershell
ConvertFrom-HtmlAttributes -Url <uri> [-Tag <string>] [-Class <string>] [-Id <string>] [-Name <string>] [-Proxy <string>] [-ProxyCredential <pscredential>] [-ReturnObject] [<CommonParameters>]
```

## DESCRIPTION
Input can be raw HTML or a page retrieved from -Url. Use
-Proxy when the page must be downloaded through a proxy server.

## EXAMPLES

### EXAMPLE 1
```powershell
ConvertFrom-HtmlAttributes -Url https://example.com -Tag a
```


### EXAMPLE 2
```powershell
ConvertFrom-HtmlAttributes -Url https://example.com -Proxy http://proxy:8080 -Tag a
```


## PARAMETERS

### -Class
Class name to search for.

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

### -Id
ID attribute to search for.

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

### -Name
Name attribute to search for.

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
Proxy server address used when Url is specified.
Include protocol and port if necessary.

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

### -ProxyCredential
Credentials used with the Proxy server.

```yaml
Type: PSCredential
Parameter Sets: Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -ReturnObject
Return matching IElement objects instead of text.

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

### -Tag
Tag name to search for.

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

### -Url
URL of HTML page to download.

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

- `System.String
AngleSharp.Dom.IElement`

## RELATED LINKS

- None
