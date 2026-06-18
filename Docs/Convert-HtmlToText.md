---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/PSParseHTML
schema: 2.0.0
---
# Convert-HtmlToText
## SYNOPSIS
Converts HTML content to plain text.

## SYNTAX
### Content (Default)
```powershell
Convert-HtmlToText -Content <string> [-OutputFile <string>] [-Readable] [-Selector <string>] [-IncludeMetadata] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

### File
```powershell
Convert-HtmlToText -Path <string> [-OutputFile <string>] [-Readable] [-Selector <string>] [-IncludeMetadata] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

### Url
```powershell
Convert-HtmlToText -Url <uri> [-OutputFile <string>] [-Readable] [-Selector <string>] [-IncludeMetadata] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

## DESCRIPTION
You can provide raw HTML, a local file, or download a page with -Url.
When downloading, optional -Proxy and -ProxyCredential can be used.

## EXAMPLES

### EXAMPLE 1
```powershell
Convert-HtmlToText -Content "<p>Hello</p>"
```


### EXAMPLE 2
```powershell
Convert-HtmlToText -Url https://example.com -Proxy http://proxy:8080
```


## PARAMETERS

### -Content
HTML content to convert.

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

### -IncludeMetadata
Return readable extraction metadata instead of only the extracted text.

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

### -OutputFile
Optional path to write the resulting text.

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
Path to a HTML file.

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
Proxy server address used when downloading from Url.
Include the protocol and port if necessary.

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
Credentials used for the Proxy server.

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

### -Readable
Select the most readable article-like content region before converting to text.

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

### -Selector
Preferred CSS selector for the readable content container.

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
URL of a HTML page to download and convert.

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

- `System.String`

## RELATED LINKS

- None
