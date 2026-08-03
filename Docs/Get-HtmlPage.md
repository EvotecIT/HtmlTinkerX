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
Get-HtmlPage [-Content] <string> [-BaseUrl <uri>] [-CollectionHint <string>] [-MinimumRepeatCount <int>] [-CollectionLimit <int>] [-NoCollections] [<CommonParameters>]
```

### File
```powershell
Get-HtmlPage [-Path] <string> [-BaseUrl <uri>] [-CollectionHint <string>] [-MinimumRepeatCount <int>] [-CollectionLimit <int>] [-NoCollections] [<CommonParameters>]
```

### Url
```powershell
Get-HtmlPage [-Url] <uri> [-CollectionHint <string>] [-MinimumRepeatCount <int>] [-CollectionLimit <int>] [-NoCollections] [-Proxy <string>] [-ProxyCredential <pscredential>] [-UserAgent <string>] [-Header <hashtable>] [<CommonParameters>]
```

### Snapshot
```powershell
Get-HtmlPage [-RenderedSnapshot] <HtmlRenderedPageSnapshot> [-CollectionHint <string>] [-MinimumRepeatCount <int>] [-CollectionLimit <int>] [-NoCollections] [<CommonParameters>]
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
