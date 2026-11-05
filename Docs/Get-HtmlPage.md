---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Get-HtmlPage
## SYNOPSIS
Reads an HTML page as headings, paragraphs, tables, links, resources, and inferred object collections.

## SYNTAX
### Content (Default)
```powershell
Get-HtmlPage [-Content] <string> [-BaseUrl <uri>] [-CollectionHint <string>] [-MinimumRepeatCount <int>] [-CollectionLimit <int>] [-NoCollections] [-NoReadableText] [-NoMarkdown] [-NoWebData] [<CommonParameters>]
```

### File
```powershell
Get-HtmlPage [-Path] <string> [-BaseUrl <uri>] [-CollectionHint <string>] [-MinimumRepeatCount <int>] [-CollectionLimit <int>] [-NoCollections] [-NoReadableText] [-NoMarkdown] [-NoWebData] [<CommonParameters>]
```

### Url
```powershell
Get-HtmlPage [-Url] <uri> [-CollectionHint <string>] [-MinimumRepeatCount <int>] [-CollectionLimit <int>] [-NoCollections] [-NoReadableText] [-NoMarkdown] [-NoWebData] [-Proxy <string[]>] [-ProxyCredential <pscredential>] [-UserAgent <string>] [-Header <hashtable>] [-RetryCount <int>] [-BrowserHeaders] [-RenderWhenBlocked] [<CommonParameters>]
```

### Snapshot
```powershell
Get-HtmlPage [-RenderedSnapshot] <HtmlRenderedPageSnapshot> [-CollectionHint <string>] [-MinimumRepeatCount <int>] [-CollectionLimit <int>] [-NoCollections] [-NoReadableText] [-NoMarkdown] [-NoWebData] [<CommonParameters>]
```

## DESCRIPTION
Reads an HTML page as headings, paragraphs, tables, links, resources, and inferred object collections.

## EXAMPLES

### EXAMPLE 1
```powershell
$page = Get-HtmlPage -Url https://example.org; $page.Headings; $page.Collections
```


### EXAMPLE 2
```powershell
Get-HtmlPage -Url https://support.microsoft.com/kb/5127692 -BrowserHeaders -RetryCount 2 -RenderWhenBlocked
```

Reads a page behind a CDN, rendering it in a browser only when the CDN blocks the request.

### EXAMPLE 3
```powershell
try { Get-HtmlPage -Url $url } catch { $_.TargetObject.Kind; $_.TargetObject.Provider; $_.TargetObject.Excerpt }
```

Reports why a page was refused.

### EXAMPLE 4
```powershell
$page = Get-HtmlPage -Content $html; $page.Collections[0].Items
```


## PARAMETERS

### -BaseUrl
Base URL used to resolve relative links and resources.

```yaml
Type: Uri
Parameter Sets: Content, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -BrowserHeaders
Sends a consistent set of desktop Chrome headers: User-Agent, Accept, Accept-Language and client hints. Cannot be combined with UserAgent, or with Header values for the headers it sets.

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

### -CollectionHint
Optional plain-text hint used to focus repeated-collection discovery.

```yaml
Type: String
Parameter Sets: Content, File, Url, Snapshot
Aliases: Query
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -CollectionLimit
Maximum number of distinct inferred collections.

```yaml
Type: Int32
Parameter Sets: Content, File, Url, Snapshot
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Content
HTML content to read.

```yaml
Type: String
Parameter Sets: Content
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Header
Additional or replacement HTTP headers used when downloading Url.

```yaml
Type: Hashtable
Parameter Sets: Url
Aliases: Headers
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MinimumRepeatCount
Minimum number of repeated elements required for an inferred collection.

```yaml
Type: Int32
Parameter Sets: Content, File, Url, Snapshot
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -NoCollections
Skips repeated-collection inference.

```yaml
Type: SwitchParameter
Parameter Sets: Content, File, Url, Snapshot
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -NoMarkdown
Skips Markdown conversion. Semantic content remains available.

```yaml
Type: SwitchParameter
Parameter Sets: Content, File, Url, Snapshot
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -NoReadableText
Skips readable-text analysis and its title fallback. Semantic content remains available.

```yaml
Type: SwitchParameter
Parameter Sets: Content, File, Url, Snapshot
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -NoWebData
Skips normalized links, forms and assets. Semantic resources and collection inference are independent.

```yaml
Type: SwitchParameter
Parameter Sets: Content, File, Url, Snapshot
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
Path to an HTML file to read.

```yaml
Type: String
Parameter Sets: File
Aliases: File
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Proxy
Proxy server address used when downloading by URL. Give several to send each attempt through the next one.

```yaml
Type: String[]
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

### -RenderedSnapshot
Rendered browser snapshot to read instead of the static source HTML.

```yaml
Type: HtmlRenderedPageSnapshot
Parameter Sets: Snapshot
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RetryCount
Retries after a transient status (408, 429, 500, 502, 503, 504) or a dropped connection, honouring Retry-After up to 30 seconds. Defaults to 1 with BrowserHeaders, RenderWhenBlocked or several proxies, otherwise 0.

```yaml
Type: Int32
Parameter Sets: Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: 0, or 1 with -BrowserHeaders, -RenderWhenBlocked or several -Proxy values
Accept pipeline input: False
Accept wildcard characters: False
```

### -Url
URL of an HTML page to download and read.

```yaml
Type: Uri
Parameter Sets: Url
Aliases: Uri
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -UserAgent
User-Agent header used when downloading Url.

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `System.String`

## OUTPUTS

- `System.Management.Automation.PSObject`

## RELATED LINKS

- None
