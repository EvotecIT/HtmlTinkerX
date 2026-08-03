---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Invoke-HtmlExtractionRecipe
## SYNOPSIS
Executes a browserless extraction recipe.

## SYNTAX
### Recipe (Default)
```powershell
Invoke-HtmlExtractionRecipe [-Recipe] <HtmlBrowserlessExtractionRecipe> [-AllowHttpFetch] [-AllowMediumRiskEndpoint] [-AllowExternalEndpoint] [-IncludeRawContent] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

### Path
```powershell
Invoke-HtmlExtractionRecipe [-Path] <string> [-AllowHttpFetch] [-AllowMediumRiskEndpoint] [-AllowExternalEndpoint] [-IncludeRawContent] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

## DESCRIPTION
Executes a browserless extraction recipe.

## EXAMPLES

### EXAMPLE 1
```powershell
Invoke-HtmlExtractionRecipe -Path .\recipe.json -AllowHttpFetch
```


## PARAMETERS

### -AllowExternalEndpoint
Allows external endpoint recipes when HTTP fetch is enabled.

```yaml
Type: SwitchParameter
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -AllowHttpFetch
Allows direct HTTP GET extraction for endpoint recipes.

```yaml
Type: SwitchParameter
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -AllowMediumRiskEndpoint
Allows medium-risk endpoint recipes when HTTP fetch is enabled.

```yaml
Type: SwitchParameter
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeRawContent
Includes raw payload or response content in the result.

```yaml
Type: SwitchParameter
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
Recipe JSON path.

```yaml
Type: String
Parameter Sets: Path
Aliases: File
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Proxy
Proxy server address used when direct HTTP extraction is enabled.

```yaml
Type: String
Parameter Sets: Recipe, Path
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
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Recipe
Recipe object to execute.

```yaml
Type: HtmlBrowserlessExtractionRecipe
Parameter Sets: Recipe
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `HtmlTinkerX.HtmlBrowserlessExtractionRecipe`

## OUTPUTS

- `HtmlTinkerX.HtmlBrowserlessExtractionResult`

## RELATED LINKS

- None
