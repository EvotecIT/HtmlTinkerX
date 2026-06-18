---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/PSParseHTML
schema: 2.0.0
---
# Export-HtmlBrowserRecipe
## SYNOPSIS
Exports a browser recipe object or current session recording snapshot to JSON.

## SYNTAX
### Recipe (Default)
```powershell
Export-HtmlBrowserRecipe [-Recipe] <HtmlBrowserRecipe> [-Path] <string> [-PassThru] [-VariableTemplatePath <string>] [-IncludeOptionalVariables] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

### Session
```powershell
Export-HtmlBrowserRecipe [[-Session] <HtmlBrowserSession>] [-Path] <string> [-PassThru] [-VariableTemplatePath <string>] [-IncludeOptionalVariables] [-HardenSelectors] [-SelectorAlternateLimit <int>] [-ReplaceSelectorAlternates] [-HardeningReportPath <string>] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

## DESCRIPTION
Exports a browser recipe object or current session recording snapshot to JSON.

## EXAMPLES

### EXAMPLE 1
```powershell
Export-HtmlBrowserRecipe -Recipe $recipe -Path .\browser.recipe.json -VariableTemplatePath .\browser.recipe.variables.json
```


### EXAMPLE 2
```powershell
Export-HtmlBrowserRecipe -Session $session -Path .\browser.recipe.json -HardenSelectors
```


## PARAMETERS

### -CancellationToken
Token used to cancel the operation.

```yaml
Type: CancellationToken
Parameter Sets: Recipe, Session
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -HardeningReportPath
Optional JSON path where a redacted selector hardening report should be written.

```yaml
Type: String
Parameter Sets: Session
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -HardenSelectors
Add safe selector alternates from the current page before saving a session recording snapshot.

```yaml
Type: SwitchParameter
Parameter Sets: Session
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -IncludeOptionalVariables
Include optional ValueVariable entries that already have stored fallback values in the variable template.

```yaml
Type: SwitchParameter
Parameter Sets: Recipe, Session
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -PassThru
Return the recipe object instead of the output path.

```yaml
Type: SwitchParameter
Parameter Sets: Recipe, Session
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Path
Output JSON path.

```yaml
Type: String
Parameter Sets: Recipe, Session
Aliases: None
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Recipe
Recipe object to export.

```yaml
Type: HtmlBrowserRecipe
Parameter Sets: Recipe
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: True
```

### -ReplaceSelectorAlternates
Replace existing selector alternates during hardening instead of appending missing alternates.

```yaml
Type: SwitchParameter
Parameter Sets: Session
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -SelectorAlternateLimit
Maximum selector alternates to keep per selector-based step when HardenSelectors is used.

```yaml
Type: Int32
Parameter Sets: Session
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Session
Session whose active or stopped recording should be exported.

```yaml
Type: HtmlBrowserSession
Parameter Sets: Session
Aliases: None
Possible values:

Required: False
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: True
```

### -VariableTemplatePath
Optional JSON path where a runtime variable template should be written.

```yaml
Type: String
Parameter Sets: Recipe, Session
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

- `HtmlTinkerX.HtmlBrowserRecipe
HtmlTinkerX.HtmlBrowserSession`

## OUTPUTS

- `System.String
HtmlTinkerX.HtmlBrowserRecipe`

## RELATED LINKS

- None
