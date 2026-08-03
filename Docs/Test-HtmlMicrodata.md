---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Test-HtmlMicrodata
## SYNOPSIS
Validates microdata items against built-in schema definitions.

## SYNTAX
### Items (Default)
```powershell
Test-HtmlMicrodata -Items <HtmlMicrodataItem[]> [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

### Content
```powershell
Test-HtmlMicrodata -Content <string> [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

### Url
```powershell
Test-HtmlMicrodata -Url <uri> [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

## DESCRIPTION
Validates microdata items against built-in schema definitions.

## EXAMPLES

### EXAMPLE 1
```powershell
Test-HtmlMicrodata -Content $html
```


## PARAMETERS

### -Content
HTML markup containing microdata.

```yaml
Type: String
Parameter Sets: Content
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByPropertyName)
Accept wildcard characters: False
```

### -Items
Microdata items to validate.

```yaml
Type: HtmlMicrodataItem[]
Parameter Sets: Items
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Proxy
Proxy server address when downloading by URL.

```yaml
Type: String
Parameter Sets: Items, Content, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ProxyCredential
Credentials for the proxy server.

```yaml
Type: PSCredential
Parameter Sets: Items, Content, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Url
URL of a page with microdata.

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

- `HtmlTinkerX.HtmlMicrodataItem[]`
- `System.String`

## OUTPUTS

- `HtmlTinkerX.MicrodataSchemaMismatch`

## RELATED LINKS

- None
