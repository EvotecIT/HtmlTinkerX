---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/PSParseHTML
schema: 2.0.0
---
# Invoke-HtmlDataExtraction
## SYNOPSIS
Extracts a browserless data source discovered by Find-HtmlDataSource.

## SYNTAX
### __AllParameterSets
```powershell
Invoke-HtmlDataExtraction [-DataSource] <HtmlBrowserlessDataSource> [-AllowHttpFetch] [-AllowMediumRiskEndpoint] [-AllowExternalEndpoint] [-IncludeRawContent] [-MaxResponseBytes <int>] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

## DESCRIPTION
Extracts a browserless data source discovered by Find-HtmlDataSource.

## EXAMPLES

### EXAMPLE 1
```powershell
Find-HtmlDataSource -Content $html -DirectOnly | Select-Object -First 1 | Invoke-HtmlDataExtraction
```


### EXAMPLE 2
```powershell
Find-HtmlDataSource -Url https://example.org/products -DirectOnly | Invoke-HtmlDataExtraction -AllowHttpFetch
```


## PARAMETERS

### -AllowExternalEndpoint
Allows external endpoint sources when HTTP fetch is enabled.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -AllowHttpFetch
Allows direct HTTP GET extraction for endpoint sources.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -AllowMediumRiskEndpoint
Allows medium-risk endpoint sources when HTTP fetch is enabled.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -DataSource
Browserless data source to extract.

```yaml
Type: HtmlBrowserlessDataSource
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: True
```

### -IncludeRawContent
Includes raw payload or response content in the result.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -MaxResponseBytes
Maximum response body size to keep from direct HTTP endpoint extraction.

```yaml
Type: Int32
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Proxy
Proxy server address used when direct HTTP extraction is enabled.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
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
Parameter Sets: __AllParameterSets
Aliases: None
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

- `HtmlTinkerX.HtmlBrowserlessDataSource`

## OUTPUTS

- `HtmlTinkerX.HtmlBrowserlessExtractionResult`

## RELATED LINKS

- None
