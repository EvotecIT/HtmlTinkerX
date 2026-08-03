---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# ConvertFrom-HtmlSyndication
## SYNOPSIS
Extracts normalized items from RSS or Atom feed XML.

## SYNTAX
### Content (Default)
```powershell
ConvertFrom-HtmlSyndication -Content <string> [-BaseUrl <uri>] [-SourceFeedUrl <string>] [<CommonParameters>]
```

### File
```powershell
ConvertFrom-HtmlSyndication -Path <string> [-BaseUrl <uri>] [-SourceFeedUrl <string>] [<CommonParameters>]
```

### Url
```powershell
ConvertFrom-HtmlSyndication -Url <uri> [-BaseUrl <uri>] [-SourceFeedUrl <string>] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

## DESCRIPTION
Extracts normalized items from RSS or Atom feed XML.

## EXAMPLES

### EXAMPLE 1
```powershell
ConvertFrom-HtmlSyndication -Content 'Value'
```


### EXAMPLE 2
```powershell
ConvertFrom-HtmlSyndication -Path 'C:\Path'
```


### EXAMPLE 3
```powershell
ConvertFrom-HtmlSyndication -Url 'Value'
```


## PARAMETERS

### -BaseUrl
Base URL used to resolve relative item URLs. Defaults to Url when downloading.

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
RSS or Atom XML content.

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
Path to an RSS or Atom XML file.

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

### -SourceFeedUrl
URL recorded as the source feed for returned items. Defaults to Url when downloading.

```yaml
Type: String
Parameter Sets: Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Url
URL of an RSS or Atom XML document.

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

- `HtmlTinkerX.HtmlSyndicationItem`

## RELATED LINKS

- None
