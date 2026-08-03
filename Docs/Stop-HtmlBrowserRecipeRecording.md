---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Stop-HtmlBrowserRecipeRecording
## SYNOPSIS
Stops a browser recipe recording and optionally saves the captured recipe to JSON.

## SYNTAX
### __AllParameterSets
```powershell
Stop-HtmlBrowserRecipeRecording [[-Session] <HtmlBrowserSession>] [[-Path] <string>] [-PassThru] [-VariableTemplatePath <string>] [-IncludeOptionalVariables] [-HardenSelectors] [-SelectorAlternateLimit <int>] [-ReplaceSelectorAlternates] [-HardeningReportPath <string>] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

## DESCRIPTION
Stops a browser recipe recording and optionally saves the captured recipe to JSON.

## EXAMPLES

### EXAMPLE 1
```powershell
Stop-HtmlBrowserRecipeRecording -Session $session -Path .\browser.recipe.json -VariableTemplatePath .\browser.recipe.variables.json
```


### EXAMPLE 2
```powershell
Stop-HtmlBrowserRecipeRecording -Session $session -Path .\browser.recipe.json -HardenSelectors
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

### -HardeningReportPath
Optional JSON path where a redacted selector hardening report should be written.

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

### -HardenSelectors
Add safe selector alternates from the current page before saving or returning the recipe.

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

### -IncludeOptionalVariables
Include optional ValueVariable entries that already have stored fallback values in the variable template.

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

### -PassThru
Return the recipe object after saving. When no path is supplied, the recipe is always returned.

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

### -Path
Optional JSON path where the recorded recipe should be saved.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ReplaceSelectorAlternates
Replace existing selector alternates during hardening instead of appending missing alternates.

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

### -SelectorAlternateLimit
Maximum selector alternates to keep per selector-based step when HardenSelectors is used.

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

### -Session
Browser session being recorded. When omitted, the default PSParseHTML session is used.

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

### -VariableTemplatePath
Optional JSON path where a runtime variable template should be written.

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `HtmlTinkerX.HtmlBrowserSession`

## OUTPUTS

- `HtmlTinkerX.HtmlBrowserRecipe`
- `System.String`

## RELATED LINKS

- None
