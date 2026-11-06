---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# ConvertFrom-HtmlTable
## SYNOPSIS
Converts HTML tables into PowerShell objects.

## SYNTAX
### Content (Default)
```powershell
ConvertFrom-HtmlTable -Content <string> [-ReplaceContent <IDictionary>] [-ReplaceHeaders <IDictionary>] [-Engine <HtmlParserEngine>] [-ReverseTable] [-IncludeMetadata] [-AsDataTable] [-AsDataSet] [-TableName <string>] [-DataSetName <string>] [-InferTypes] [-IncludeLinkUrls] [-MaximumColumns <int>] [-MaximumRows <int>] [-MaximumExpandedCells <long>] [-TableIndex <int[]>] [-TableId <string>] [-TableClass <string>] [-Caption <string>] [-Header <string>] [-AllProperties] [-EmptyValuePlaceholder <string>] [-CleanHeaders] [-SkipFooter] [-CellTextFormat <string>] [-Proxy <string[]>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

### File
```powershell
ConvertFrom-HtmlTable -Path <string> [-ReplaceContent <IDictionary>] [-ReplaceHeaders <IDictionary>] [-Engine <HtmlParserEngine>] [-ReverseTable] [-IncludeMetadata] [-AsDataTable] [-AsDataSet] [-TableName <string>] [-DataSetName <string>] [-InferTypes] [-IncludeLinkUrls] [-MaximumColumns <int>] [-MaximumRows <int>] [-MaximumExpandedCells <long>] [-TableIndex <int[]>] [-TableId <string>] [-TableClass <string>] [-Caption <string>] [-Header <string>] [-AllProperties] [-EmptyValuePlaceholder <string>] [-CleanHeaders] [-SkipFooter] [-CellTextFormat <string>] [-Proxy <string[]>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

### Url
```powershell
ConvertFrom-HtmlTable -Url <uri> [-ReplaceContent <IDictionary>] [-ReplaceHeaders <IDictionary>] [-Engine <HtmlParserEngine>] [-ReverseTable] [-IncludeMetadata] [-AsDataTable] [-AsDataSet] [-TableName <string>] [-DataSetName <string>] [-InferTypes] [-IncludeLinkUrls] [-MaximumColumns <int>] [-MaximumRows <int>] [-MaximumExpandedCells <long>] [-TableIndex <int[]>] [-TableId <string>] [-TableClass <string>] [-Caption <string>] [-Header <string>] [-AllProperties] [-EmptyValuePlaceholder <string>] [-CleanHeaders] [-SkipFooter] [-CellTextFormat <string>] [-Proxy <string[]>] [-ProxyCredential <pscredential>] [-RetryCount <int>] [-BrowserHeaders] [-RenderWhenBlocked] [-ResumeInBrowser] [-ResumeTimeoutSeconds <int>] [-CdpEndpointUrl <string>] [-ReplayCache <string>] [<CommonParameters>]
```

## DESCRIPTION
The cmdlet accepts raw HTML or downloads a page using -Url. When
downloading you can specify -Proxy and -ProxyCredential.

## EXAMPLES

### EXAMPLE 1
```powershell
ConvertFrom-HtmlTable -Url https://example.com
```


### EXAMPLE 2
```powershell
ConvertFrom-HtmlTable -Url https://example.com -Proxy http://proxy:8080
```


## PARAMETERS

### -AllProperties
Pad rows with missing cells.

```yaml
Type: SwitchParameter
Parameter Sets: Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -AsDataSet
Return all parsed HTML tables as a DataSet.

```yaml
Type: SwitchParameter
Parameter Sets: Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -AsDataTable
Return each parsed HTML table as a DataTable.

```yaml
Type: SwitchParameter
Parameter Sets: Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

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

### -Caption
Caption text fragment to include.

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

### -CellTextFormat
Controls how cell text is extracted (Compact, Lines, Markdown).

```yaml
Type: String
Parameter Sets: Content, File, Url
Aliases: None
Possible values: Compact, Lines, Markdown

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -CleanHeaders
Automatically clean special characters from header names that can cause PowerShell formatting issues.

```yaml
Type: SwitchParameter
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
HTML content containing tables.

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

### -DataSetName
Name to use when returning a DataSet.

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

### -EmptyValuePlaceholder
Value to use for empty cells to improve PowerShell formatting compatibility.

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

### -Engine
Selects parsing engine.

```yaml
Type: HtmlParserEngine
Parameter Sets: Content, File, Url
Aliases: None
Possible values: AngleSharp, AgilityPack

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Header
Header name to include.

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

### -IncludeLinkUrls
Add companion URL columns for linked table cells.

```yaml
Type: SwitchParameter
Parameter Sets: Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeMetadata
Include table metadata information.

```yaml
Type: SwitchParameter
Parameter Sets: Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -InferTypes
Infer simple .NET column types when returning DataTable/DataSet output.

```yaml
Type: SwitchParameter
Parameter Sets: Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaximumColumns
Maximum output columns per table, including companion link columns. Default: 10000.

```yaml
Type: Int32
Parameter Sets: Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaximumExpandedCells
Maximum expanded result cells across all tables. Default: 1000000. Raise explicitly for trusted large tables.

```yaml
Type: Int64
Parameter Sets: Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaximumRows
Maximum source rows across all tables, including headers. Default: 100000.

```yaml
Type: Int32
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
Path to an HTML file containing tables.

```yaml
Type: String
Parameter Sets: File
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByPropertyName)
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

### -ReplaceContent
Replacements to apply to table cell contents.

```yaml
Type: IDictionary
Parameter Sets: Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ReplaceHeaders
Replacements to apply to table headers.

```yaml
Type: IDictionary
Parameter Sets: Content, File, Url
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

### -ReverseTable
Interpret table rows as key/value pairs.

```yaml
Type: SwitchParameter
Parameter Sets: Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SkipFooter
Skip HTML table footer (<tfoot>) elements when parsing tables.

```yaml
Type: SwitchParameter
Parameter Sets: Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -TableClass
CSS class token to include.

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

### -TableId
Table id to include.

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

### -TableIndex
Zero-based table indexes to include.

```yaml
Type: Int32[]
Parameter Sets: Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -TableName
Name to use when returning a single DataTable.

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
URL of a page with tables.

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

- `System.Management.Automation.PSObject[]`
- `System.Data.DataTable`
- `System.Data.DataSet`

## RELATED LINKS

- None
