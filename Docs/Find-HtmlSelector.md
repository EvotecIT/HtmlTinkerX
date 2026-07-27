---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Find-HtmlSelector
## SYNOPSIS
Finds repeated static HTML structures, likely fields, links, and replayable extraction commands or secure templates.

## SYNTAX
### Content (Default)
```powershell
Find-HtmlSelector [-Content] <string> [[-Query] <string>] [-BaseUrl <uri>] [-MinimumRepeatCount <int>] [-Limit <int>] [<CommonParameters>]
```

### File
```powershell
Find-HtmlSelector [-Path] <string> [[-Query] <string>] [-BaseUrl <uri>] [-MinimumRepeatCount <int>] [-Limit <int>] [<CommonParameters>]
```

### Url
```powershell
Find-HtmlSelector [-Url] <uri> [[-Query] <string>] [-MinimumRepeatCount <int>] [-Limit <int>] [-Proxy <string>] [-ProxyCredential <pscredential>] [-UserAgent <string>] [-Header <hashtable>] [<CommonParameters>]
```

## DESCRIPTION
Finds repeated static HTML structures, likely fields, links, and replayable extraction commands or secure templates.

## EXAMPLES

### EXAMPLE 1
```powershell
Find-HtmlSelector -Url https://example.org/products -Query 'Product'
```


### EXAMPLE 2
```powershell
$candidate = Find-HtmlSelector -Content $html -Limit 1; $candidate.Fields
```


## PARAMETERS

### -BaseUrl
Base URL used to resolve relative link and image samples.

```yaml
Type: Uri
Parameter Sets: Content, File
Aliases:
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
Aliases:
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: True
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
Accept wildcard characters: True
```

### -Limit
Maximum number of ranked candidates to return.

```yaml
Type: Int32
Parameter Sets: Content, File, Url
Aliases:
Possible values:

Required: False
Position: named
Default value: 10
Accept pipeline input: False
Accept wildcard characters: True
```

### -MinimumRepeatCount
Minimum number of repeated elements a candidate selector must match.

```yaml
Type: Int32
Parameter Sets: Content, File, Url
Aliases:
Possible values:

Required: False
Position: named
Default value: 2
Accept pipeline input: False
Accept wildcard characters: True
```

### -Path
Path to an HTML file to inspect.

```yaml
Type: String
Parameter Sets: File
Aliases: File
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Proxy
Proxy server address used when downloading by URL.

```yaml
Type: String
Parameter Sets: Url
Aliases:
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
Aliases:
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Query
Optional visible text, URL, id, class, or attribute fragment used to focus discovery.

```yaml
Type: String
Parameter Sets: Content, File, Url
Aliases:
Possible values:

Required: False
Position: 1
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
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -UserAgent
User-Agent header used when downloading Url.

```yaml
Type: String
Parameter Sets: Url
Aliases:
Possible values:

Required: False
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

- `HtmlTinkerX.HtmlDomSelectorCandidate`

## RELATED LINKS

- None
