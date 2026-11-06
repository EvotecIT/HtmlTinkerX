---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Convert-HtmlToMarkdown
## SYNOPSIS
Converts HTML content to Markdown.

## SYNTAX
### Content (Default)
```powershell
Convert-HtmlToMarkdown -Content <string> [-PageUrl <string>] [-OutputFile <string>] [-MarkdownProfile <HtmlMarkdownProfile>] [-MarkdownImageMode <MarkdownImageRenderingMode>] [-ListingCardMetadataMode <HtmlListingCardMetadataMode>] [-Proxy <string[]>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

### File
```powershell
Convert-HtmlToMarkdown -Path <string> [-PageUrl <string>] [-OutputFile <string>] [-MarkdownProfile <HtmlMarkdownProfile>] [-MarkdownImageMode <MarkdownImageRenderingMode>] [-ListingCardMetadataMode <HtmlListingCardMetadataMode>] [-Proxy <string[]>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

### Url
```powershell
Convert-HtmlToMarkdown -Url <uri> [-PageUrl <string>] [-OutputFile <string>] [-MarkdownProfile <HtmlMarkdownProfile>] [-MarkdownImageMode <MarkdownImageRenderingMode>] [-ListingCardMetadataMode <HtmlListingCardMetadataMode>] [-Proxy <string[]>] [-ProxyCredential <pscredential>] [-RetryCount <int>] [-BrowserHeaders] [-RenderWhenBlocked] [-ResumeInBrowser] [-ResumeTimeoutSeconds <int>] [-CdpEndpointUrl <string>] [-ReplayCache <string>] [<CommonParameters>]
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

### -BrowserHeaders
Sends a consistent set of browser headers: User-Agent, Accept, Accept-Language and client hints. With RenderWhenBlocked or ResumeInBrowser they are read from that browser, so the HTTP requests and the browser present one identity; otherwise they describe an ordinary desktop Chrome. Cannot be combined with UserAgent, or with Header values for the headers it sets.

```yaml
Type: SwitchParameter
Parameter Sets: Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -CdpEndpointUrl
Chrome DevTools Protocol endpoint of your own running Chrome, used by RenderWhenBlocked and ResumeInBrowser instead of a launched browser.

```yaml
Type: String
Parameter Sets: Url
Aliases: CdpEndpoint, RemoteDebuggingUrl
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

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
Accept wildcard characters: False
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
Accept wildcard characters: False
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
Accept wildcard characters: False
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
Accept wildcard characters: False
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
Accept wildcard characters: False
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
Accept wildcard characters: False
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
Accept wildcard characters: False
```

### -Proxy
Proxy server address used when downloading by URL. Give several to send each attempt through the next one.

```yaml
Type: String[]
Parameter Sets: Content, File, Url
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
Parameter Sets: Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RenderWhenBlocked
Renders the page in a headless browser, through the first proxy, when a Cloudflare challenge or a recognised CDN block page is returned.

```yaml
Type: SwitchParameter
Parameter Sets: Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ReplayCache
Directory of a development cache that stores whole responses and replays them without fetching the site again. Requests with cookies, credentials or custom headers are not cached. Not for production reads: stored responses never expire.

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

### -ResumeInBrowser
When the site challenges or blocks the request, opens the page in a visible browser so you can complete what the site asks, then continues with that browser's cookies for the site. Nothing is solved automatically. Combine with BrowserHeaders so the cookies are used with the browser's own identity.

```yaml
Type: SwitchParameter
Parameter Sets: Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ResumeTimeoutSeconds
Seconds ResumeInBrowser waits for you before the request fails as blocked.

```yaml
Type: Int32
Parameter Sets: Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RetryCount
Retries after a transient status (408, 429, 500, 502, 503, 504) or a dropped connection, honouring Retry-After up to 30 seconds. Defaults to 1 with BrowserHeaders, RenderWhenBlocked, ResumeInBrowser or several proxies, otherwise 0.

```yaml
Type: Int32
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
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `System.String`

## OUTPUTS

- `System.String`

## RELATED LINKS

- None
