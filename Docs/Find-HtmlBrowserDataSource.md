---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Find-HtmlBrowserDataSource
## SYNOPSIS
Finds browserless extraction candidates from observed browser network traffic.

## SYNTAX
### __AllParameterSets
```powershell
Find-HtmlBrowserDataSource [[-Session] <HtmlBrowserSession>] [-PageUrl <string>] [-ResourceType <HtmlNetworkResourceType[]>] [-IncludeDocument] [-IncludeFailed] [-IncludeNonGet] [-IncludeExternal] [-IncludeResponseBody] [-RedactResponseBody] [-ResponseBodyMaxBytes <int>] [-ResponseBodyResourceType <HtmlNetworkResourceType[]>] [-MaxSource <int>] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

## DESCRIPTION
Finds browserless extraction candidates from observed browser network traffic.

## EXAMPLES

### EXAMPLE 1
```powershell
$session | Find-HtmlBrowserDataSource -IncludeResponseBody | Export-HtmlExtractionRecipe -Path .\recipe.json
```


## PARAMETERS

### -CancellationToken
Token used to cancel the operation.

```yaml
Type: CancellationToken
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeDocument
Also include document requests.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeExternal
Include endpoints outside the page origin.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeFailed
Include failed or non-successful requests in the output.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeNonGet
Include non-GET requests. They are classified as higher risk and are not fetched automatically.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeResponseBody
Copy captured response bodies into output sources when available.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaxSource
Maximum number of data-source candidates returned. Zero means no limit.

```yaml
Type: Int32
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PageUrl
Override the page URL used for same-origin checks.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RedactResponseBody
Redact common tokens, passwords, and secrets before copied response bodies are exposed as data-source content.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ResourceType
Browser resource types considered as data-source candidates. Defaults to XHR and Fetch.

```yaml
Type: HtmlNetworkResourceType[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values: Document, Stylesheet, Image, Media, Font, Script, TextTrack, XHR, Fetch, EventSource, WebSocket, Manifest, Other

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ResponseBodyMaxBytes
Maximum UTF-8 bytes captured per response body when IncludeResponseBody is used.

```yaml
Type: Int32
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ResponseBodyResourceType
Resource types whose response bodies should be captured. Defaults to XHR and Fetch.

```yaml
Type: HtmlNetworkResourceType[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values: Document, Stylesheet, Image, Media, Font, Script, TextTrack, XHR, Fetch, EventSource, WebSocket, Manifest, Other

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Session
Browser session containing network traffic. When omitted, the default PSParseHTML session is used.

```yaml
Type: HtmlBrowserSession
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `HtmlTinkerX.HtmlBrowserSession`

## OUTPUTS

- `HtmlTinkerX.HtmlBrowserlessDataSource`

## RELATED LINKS

- None
