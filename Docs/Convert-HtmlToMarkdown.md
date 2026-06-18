---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/PSParseHTML
schema: 2.0.0
---
# Convert-HtmlToMarkdown
## SYNOPSIS
Converts HTML content to Markdown.

## SYNTAX
### Content (Default)
```powershell
Convert-HtmlToMarkdown -Content <string> [-PageUrl <string>] [-OutputFile <string>] [-MarkdownProfile <HtmlMarkdownProfile>] [-MarkdownImageMode <MarkdownImageRenderingMode>] [-ListingCardMetadataMode <HtmlListingCardMetadataMode>] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

### File
```powershell
Convert-HtmlToMarkdown -Path <string> [-PageUrl <string>] [-OutputFile <string>] [-MarkdownProfile <HtmlMarkdownProfile>] [-MarkdownImageMode <MarkdownImageRenderingMode>] [-ListingCardMetadataMode <HtmlListingCardMetadataMode>] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

### Url
```powershell
Convert-HtmlToMarkdown -Url <uri> [-PageUrl <string>] [-OutputFile <string>] [-MarkdownProfile <HtmlMarkdownProfile>] [-MarkdownImageMode <MarkdownImageRenderingMode>] [-ListingCardMetadataMode <HtmlListingCardMetadataMode>] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

## DESCRIPTION
You can provide raw HTML, a local file, or download a page with -Url.
When downloading, optional -Proxy and -ProxyCredential can be used.

## EXAMPLES

### EXAMPLE 1
```powershell
Convert-HtmlToMarkdown -Content "<h1>Hello</h1>"
```


### EXAMPLE 2
```powershell
Convert-HtmlToMarkdown -Url https://example.com -Proxy http://proxy:8080
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

### -ListingCardMetadataMode
Controls whether low-value metadata inside repeated listing cards should be preserved or suppressed.

```yaml
Type: HtmlListingCardMetadataMode
Parameter Sets: Content, File, Url
Aliases: None
Possible values: Preserve, SuppressInRepeatedCards

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -MarkdownImageMode
Controls how images are emitted in Markdown.

```yaml
Type: MarkdownImageRenderingMode
Parameter Sets: Content, File, Url
Aliases: None
Possible values: RichMarkdown, PortableMarkdown, Html

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -MarkdownProfile
Controls which Markdown dialect profile is used.

```yaml
Type: HtmlMarkdownProfile
Parameter Sets: Content, File, Url
Aliases: None
Possible values: Portable, OfficeIMO

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -OutputFile
Optional path to write the resulting Markdown.

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

### -PageUrl
Optional absolute page URL used to resolve relative links and images.

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
