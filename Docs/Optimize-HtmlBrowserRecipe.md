---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Optimize-HtmlBrowserRecipe
## SYNOPSIS
Hardens browser recipe selectors against the current browser page by adding safe selector alternates.

## SYNTAX
### Recipe (Default)
```powershell
Optimize-HtmlBrowserRecipe [[-Session] <HtmlBrowserSession>] [-Recipe] <HtmlBrowserRecipe> [-OutPath <string>] [-SelectorAlternateLimit <int>] [-ReplaceSelectorAlternates] [-ReportPath <string>] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

### Path
```powershell
Optimize-HtmlBrowserRecipe [[-Session] <HtmlBrowserSession>] [-Path] <string> [-OutPath <string>] [-SelectorAlternateLimit <int>] [-ReplaceSelectorAlternates] [-ReportPath <string>] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

## DESCRIPTION
Hardens browser recipe selectors against the current browser page by adding safe selector alternates.

## EXAMPLES

### EXAMPLE 1
```powershell
$session = Start-HtmlBrowserSession -Url https://example.org/app
Optimize-HtmlBrowserRecipe -Session $session -Path .\browser.recipe.json -OutPath .\browser.hardened.recipe.json
```


## PARAMETERS

### -CancellationToken
Token used to cancel the operation.

```yaml
Type: CancellationToken
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -OutPath
Optional path where the hardened recipe should be saved. When omitted, the input path is overwritten for Path input.

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

### -Path
Recipe JSON path to load and harden.

```yaml
Type: String
Parameter Sets: Path
Aliases: File
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Recipe
Recipe object to harden.

```yaml
Type: HtmlBrowserRecipe
Parameter Sets: Recipe
Aliases: None
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ReplaceSelectorAlternates
Replace existing selector alternates instead of appending missing alternates.

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

### -ReportPath
Optional JSON path where a redacted selector hardening report should be written.

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

### -SelectorAlternateLimit
Maximum selector alternates to keep per selector-based step.

```yaml
Type: Int32
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Session
Browser session whose current page matches the recipe state to harden.

```yaml
Type: HtmlBrowserSession
Parameter Sets: Recipe, Path
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

- `HtmlTinkerX.HtmlBrowserRecipeHardeningResult`

## RELATED LINKS

- None
