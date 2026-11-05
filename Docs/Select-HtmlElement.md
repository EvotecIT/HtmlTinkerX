---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Select-HtmlElement
## SYNOPSIS
Selects elements, text or attribute values from static HTML with a CSS selector or by text.

## SYNTAX
### Input (Default)
```powershell
Select-HtmlElement [-Input] <Object> [[-Selector] <string>] [-Text <string>] [-TextMatch <HtmlTextMatch>] [-CaseSensitive] [-Similar] [-BaseUrl <uri>] [-First] [-Required] [<CommonParameters>]
```

### Content
```powershell
Select-HtmlElement [[-Selector] <string>] -Content <string> [-Text <string>] [-TextMatch <HtmlTextMatch>] [-CaseSensitive] [-Similar] [-BaseUrl <uri>] [-First] [-Required] [<CommonParameters>]
```

### File
```powershell
Select-HtmlElement [[-Selector] <string>] -Path <string> [-Text <string>] [-TextMatch <HtmlTextMatch>] [-CaseSensitive] [-Similar] [-BaseUrl <uri>] [-First] [-Required] [<CommonParameters>]
```

### Url
```powershell
Select-HtmlElement [[-Selector] <string>] -Url <uri> [-Text <string>] [-TextMatch <HtmlTextMatch>] [-CaseSensitive] [-Similar] [-BaseUrl <uri>] [-First] [-Required] [-Proxy <string>] [-ProxyCredential <pscredential>] [-UserAgent <string>] [-Header <hashtable>] [<CommonParameters>]
```

## DESCRIPTION
Selects elements, text or attribute values from static HTML with a CSS selector or by text.

A selector that ends in `::text` returns each element's own text, and one that ends in `::attr(name)` returns attribute values.
`li *::text` (or `li ::text`) returns the own text of every element inside `li`.
URL attributes such as `href` and `src` are resolved against `-BaseUrl`, the downloaded `-Url`, or the page's `<base href>`.
Elements without own text or without the attribute are skipped.
`::text` and `::attr(name)` must be the last part of the selector and cannot be used in a selector list.

`-Text` finds elements by their own text, ignoring case unless `-CaseSensitive` is used. With `-Selector`, only matching elements are searched.

`-Similar` takes the first match and returns the other elements shaped like it (same tag and ancestor path, similar classes and children), for example the remaining items of a list.

## EXAMPLES

### EXAMPLE 1
```powershell
ConvertFrom-Html -Content $html | Select-HtmlElement -Selector '.product-card'
```


### EXAMPLE 2
```powershell
Select-HtmlElement -Url https://example.org -Selector 'h1' -First
```


### EXAMPLE 3
```powershell
Select-HtmlElement -Url https://example.org/news -Selector 'article a::attr(href)'
Select-HtmlElement -Path .\page.html -Selector 'h2::text'
```

Return absolute link targets and headline text.

### EXAMPLE 4
```powershell
Select-HtmlElement -Content $html -Text 'next' -Selector 'a::attr(href)' -BaseUrl https://example.org/list
```

Find the link labelled Next and return its target.

### EXAMPLE 5
```powershell
Select-HtmlElement -Path .\shop.html -Selector 'li.featured' -Similar
Select-HtmlElement -Path .\shop.html -Text 'Gold One' -Selector 'h2::text' -Similar
```

Return the other list items shaped like the featured one, then the titles of the items shaped like the one titled Gold One.

### EXAMPLE 6
```powershell
Select-HtmlElement -Content $html -Text '^v\d+\.\d+' -TextMatch Regex -Selector 'li'
```

Find list items by a regular expression.

## PARAMETERS

### -BaseUrl
Base URL used to resolve URL attributes selected with ::attr(name). Defaults to Url when downloading.

```yaml
Type: Uri
Parameter Sets: Input, Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -CaseSensitive
Compare -Text case-sensitively.

```yaml
Type: SwitchParameter
Parameter Sets: Input, Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Content
HTML content to search.

```yaml
Type: String
Parameter Sets: Content
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -First
Return only the first matching element.

```yaml
Type: SwitchParameter
Parameter Sets: Input, Content, File, Url
Aliases: Single
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
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

### -Input
Parsed document, element, HtmlAgilityPack node, or raw markup to search.

```yaml
Type: Object
Parameter Sets: Input
Aliases: HtmlDocument, HtmlNode, Node, InputObject
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Path
Path to an HTML file to search.

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

### -Required
Throw when nothing matches.

```yaml
Type: SwitchParameter
Parameter Sets: Input, Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Selector
CSS selector evaluated against the static document or input element. End it with ::text or ::attr(name) to return strings.

```yaml
Type: String
Parameter Sets: Input, Content, File, Url
Aliases: None
Possible values:

Required: False
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Similar
Return the elements shaped like the first match (same tag and ancestor path, similar classes and children) instead of the match itself.

```yaml
Type: SwitchParameter
Parameter Sets: Input, Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Text
Find elements whose own text matches this value. Use with -Selector to limit the candidates.

```yaml
Type: String
Parameter Sets: Input, Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -TextMatch
How -Text is compared with each element's own text: Exact, Contains or Regex.

```yaml
Type: HtmlTextMatch
Parameter Sets: Input, Content, File, Url
Aliases: None
Possible values: Exact, Contains, Regex

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Url
URL of an HTML page to download and search.

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

- `System.Object`

## OUTPUTS

- `AngleSharp.Dom.IElement`
- `System.String`

## RELATED LINKS

- None
