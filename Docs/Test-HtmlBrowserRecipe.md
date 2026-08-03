---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Test-HtmlBrowserRecipe
## SYNOPSIS
Validates a browser automation recipe before replaying it.

## SYNTAX
### Recipe (Default)
```powershell
Test-HtmlBrowserRecipe [-Recipe] <HtmlBrowserRecipe> [-Variable <IDictionary>] [-VariablePath <string>] [-AssumeSession] [-StrictPreflight] [-ThrowOnFailure] [<CommonParameters>]
```

### Path
```powershell
Test-HtmlBrowserRecipe [-Path] <string> [-Variable <IDictionary>] [-VariablePath <string>] [-AssumeSession] [-StrictPreflight] [-ThrowOnFailure] [<CommonParameters>]
```

## DESCRIPTION
Validates a browser automation recipe before replaying it.

## EXAMPLES

### EXAMPLE 1
```powershell
$validation = Test-HtmlBrowserRecipe -Path .\browser.recipe.json -VariablePath .\browser.recipe.variables.json
$validation.RequiredVariables
$validation.VariableTemplate
$validation.Issues | Format-Table Severity, StepIndex, Action, Property, Message
```


### EXAMPLE 2
```powershell
Test-HtmlBrowserRecipe -Path .\browser.recipe.json -StrictPreflight -ThrowOnFailure
```


## PARAMETERS

### -AssumeSession
Allow a missing StartUrl because replay will use an existing browser session.

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

### -Recipe
Recipe object to validate.

```yaml
Type: HtmlBrowserRecipe
Parameter Sets: Recipe
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -StrictPreflight
Treat warnings as blocking issues for validation summaries and CI gates.

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

### -ThrowOnFailure
Emit the validation result and then throw a terminating error when blocking issues are present.

```yaml
Type: SwitchParameter
Parameter Sets: Recipe, Path
Aliases: FailOnFailure, FailOnIssue
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Variable
Runtime variables that will be supplied during replay.

```yaml
Type: IDictionary
Parameter Sets: Recipe, Path
Aliases: RecipeVariable
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -VariablePath
JSON file containing runtime variables to use for validation. Placeholder values such as <secret> are treated as missing.

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `HtmlTinkerX.HtmlBrowserRecipe`

## OUTPUTS

- `HtmlTinkerX.HtmlBrowserRecipeValidationResult`

## RELATED LINKS

- None
