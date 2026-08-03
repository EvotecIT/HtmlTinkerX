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
ConvertFrom-HtmlTable -Content <string> [-ReplaceContent <IDictionary>] [-ReplaceHeaders <IDictionary>] [-Engine <HtmlParserEngine>] [-ReverseTable] [-IncludeMetadata] [-AsDataTable] [-AsDataSet] [-TableName <string>] [-DataSetName <string>] [-InferTypes] [-IncludeLinkUrls] [-TableIndex <int[]>] [-TableId <string>] [-TableClass <string>] [-Caption <string>] [-Header <string>] [-AllProperties] [-EmptyValuePlaceholder <string>] [-CleanHeaders] [-SkipFooter] [-CellTextFormat <string>] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

### File
```powershell
ConvertFrom-HtmlTable -Path <string> [-ReplaceContent <IDictionary>] [-ReplaceHeaders <IDictionary>] [-Engine <HtmlParserEngine>] [-ReverseTable] [-IncludeMetadata] [-AsDataTable] [-AsDataSet] [-TableName <string>] [-DataSetName <string>] [-InferTypes] [-IncludeLinkUrls] [-TableIndex <int[]>] [-TableId <string>] [-TableClass <string>] [-Caption <string>] [-Header <string>] [-AllProperties] [-EmptyValuePlaceholder <string>] [-CleanHeaders] [-SkipFooter] [-CellTextFormat <string>] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

### Url
```powershell
ConvertFrom-HtmlTable -Url <uri> [-ReplaceContent <IDictionary>] [-ReplaceHeaders <IDictionary>] [-Engine <HtmlParserEngine>] [-ReverseTable] [-IncludeMetadata] [-AsDataTable] [-AsDataSet] [-TableName <string>] [-DataSetName <string>] [-InferTypes] [-IncludeLinkUrls] [-TableIndex <int[]>] [-TableId <string>] [-TableClass <string>] [-Caption <string>] [-Header <string>] [-AllProperties] [-EmptyValuePlaceholder <string>] [-CleanHeaders] [-SkipFooter] [-CellTextFormat <string>] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
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
Proxy server address used when Url is specified.
Include protocol and port if required.

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

### -ProxyCredential
Credentials used for authenticating with the specified Proxy server.

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
