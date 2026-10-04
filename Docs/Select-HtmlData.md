---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Select-HtmlData
## SYNOPSIS
Selects normalized structured data, links, assets, tokens, forms, and app state from HTML.

## SYNTAX
### Node (Default)
```powershell
Select-HtmlData [-HtmlNode] <Object> [-Kind <string[]>] [-ItemSelector <string>] [-Property <IDictionary>] [-AsExtractionReport] [-MinimumItemCount <Int32>] [-MaximumItemCount <Int32>] [-BaseUrl <uri>] [<CommonParameters>]
```

### Content
```powershell
Select-HtmlData -Content <string> [-Kind <string[]>] [-ItemSelector <string>] [-Property <IDictionary>] [-AsExtractionReport] [-MinimumItemCount <Int32>] [-MaximumItemCount <Int32>] [-BaseUrl <uri>] [<CommonParameters>]
```

### File
```powershell
Select-HtmlData -Path <string> [-Kind <string[]>] [-ItemSelector <string>] [-Property <IDictionary>] [-AsExtractionReport] [-MinimumItemCount <Int32>] [-MaximumItemCount <Int32>] [-BaseUrl <uri>] [<CommonParameters>]
```

### Url
```powershell
Select-HtmlData -Url <uri> [-Kind <string[]>] [-ItemSelector <string>] [-Property <IDictionary>] [-AsExtractionReport] [-MinimumItemCount <Int32>] [-MaximumItemCount <Int32>] [-BaseUrl <uri>] [-Proxy <string>] [-ProxyCredential <pscredential>] [-UserAgent <string>] [-Header <hashtable>] [<CommonParameters>]
```

## DESCRIPTION
Selects normalized structured data, links, assets, tokens, forms, and app state from HTML.

## EXAMPLES

### EXAMPLE 1
```powershell
Select-HtmlData -Url https://example.org -BaseUrl https://example.org
```


### EXAMPLE 2
```powershell
Select-HtmlData -Content $html -Kind JsonLd,OpenGraph,Meta,Microdata
```


### EXAMPLE 3
```powershell
Select-HtmlNode -Content $html -XPath '//head' | Select-HtmlData -Kind HeadLink,Meta
```


### EXAMPLE 4
```powershell
Select-HtmlData -Url https://example.org/products -ItemSelector '.product-card' -Property @{
    Name = '.product-title'
    Price = '.product-price'
    Link = @{ Selector = 'a'; Attribute = 'href' }
}
```


### EXAMPLE 5
```powershell
Select-HtmlData -Content $html -ItemSelector '.product-card' -Property @{
    Name = @{ Selector = '.product-title'; Required = $true; TreatEmptyAsMissing = $true }
    Price = @{ Selector = '.product-price'; DataType = [decimal]; Culture = 'pl-PL' }
}
```

Price values such as 1234,50 become decimal values. Invalid prices and missing names raise an error identifying the field and item.

### EXAMPLE 6
```powershell
$report = Select-HtmlData -Content $html -ItemSelector '.product-card' -Property @{
    Name = @{ Selector = '.product-title'; Required = $true }
    Price = @{ Selector = '.product-price'; DataType = [decimal]; MaximumValueCount = 1 }
} -AsExtractionReport -MinimumItemCount 1
$report.IsValid
$report.Fields
```

Missing required fields, invalid values, and count changes are reported. Inspect IsValid before accepting Records; fields with data errors contain null.

## PARAMETERS

### -AsExtractionReport
Return a single extraction report with records, selector provenance, and field quality checks. Requires ItemSelector and Property.

```yaml
Type: SwitchParameter
Parameter Sets: Node, Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -BaseUrl
Base URL used to resolve relative links and assets. Defaults to Url when downloading.

```yaml
Type: Uri
Parameter Sets: Node, Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Content
HTML content to inspect.

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

### -HtmlNode
HtmlAgilityPack node or document to inspect.

```yaml
Type: Object
Parameter Sets: Node
Aliases: Node, InputObject
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -ItemSelector
CSS selector matching each repeated item to convert into a PowerShell object.

```yaml
Type: String
Parameter Sets: Node, Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Kind
Data families to include. Supported values include JsonLd, Microdata, OpenGraph, Meta, HeadLink, AppState, ScriptData, Token, Form, Link, and Asset.

```yaml
Type: String[]
Parameter Sets: Node, Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaximumItemCount
Maximum acceptable item count in an extraction report. Requires AsExtractionReport.

```yaml
Type: Int32
Parameter Sets: Node, Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MinimumItemCount
Minimum acceptable item count in an extraction report. Requires AsExtractionReport.

```yaml
Type: Int32
Parameter Sets: Node, Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
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
Accept wildcard characters: False
```

### -Property
Property-to-selector map used with ItemSelector.
String values read trimmed text. Hashtable values can specify Selector, Attribute,
ValueKind, All, Required, DefaultValue, ResolveUrl, DataType, Culture, TreatEmptyAsMissing,
MinimumValueCount, or MaximumValueCount.
DataType accepts [string], [int], [long], [decimal], [bool], [DateTimeOffset], or an enum type.
Conversion uses invariant culture by default; Culture can specify a name such as pl-PL.
Required fields throw when missing. Invalid typed values report the property and item index.

```yaml
Type: IDictionary
Parameter Sets: Node, Content, File, Url
Aliases: Properties, Field, Fields
Possible values:

Required: False
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
- `System.Object`

## OUTPUTS

- `HtmlTinkerX.HtmlDataItem`
- `System.Management.Automation.PSObject`
- `HtmlTinkerX.HtmlDomExtractionReport`

## RELATED LINKS

- None
