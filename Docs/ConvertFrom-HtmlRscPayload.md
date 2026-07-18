---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# ConvertFrom-HtmlRscPayload
## SYNOPSIS
Extracts inline React Server Component / React Flight payloads from HTML.

## SYNTAX
### Content (Default)
```powershell
ConvertFrom-HtmlRscPayload -Content <string> [-RawPayload] [-AsDocument] [<CommonParameters>]
```

### File
```powershell
ConvertFrom-HtmlRscPayload -Path <string> [-RawPayload] [-AsDocument] [<CommonParameters>]
```

### Url
```powershell
ConvertFrom-HtmlRscPayload -Url <uri> [-Proxy <string>] [-ProxyCredential <pscredential>] [-RawPayload] [-AsDocument] [<CommonParameters>]
```

## DESCRIPTION
Extracts inline React Server Component / React Flight payloads from HTML.

## EXAMPLES

### EXAMPLE 1
```powershell
ConvertFrom-HtmlRscPayload -Content $html
```


## PARAMETERS

### -AsDocument
Returns the full document object with both payloads and rows.

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
HTML content to inspect.

```yaml
Type: String
Parameter Sets: Content
Aliases: Html
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue, ByPropertyName)
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
Accept wildcard characters: True
```

### -RawPayload
Returns raw Next.js inline payload instructions instead of decoded rows.

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
Accept wildcard characters: True
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `System.String`

## OUTPUTS

- `HtmlTinkerX.HtmlReactFlightRow
HtmlTinkerX.HtmlReactFlightPayload
HtmlTinkerX.HtmlReactFlightDocument`

## RELATED LINKS

- None
