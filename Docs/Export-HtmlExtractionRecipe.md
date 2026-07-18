---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Export-HtmlExtractionRecipe
## SYNOPSIS
Saves a browserless extraction recipe created from a discovered data source.

## SYNTAX
### __AllParameterSets
```powershell
Export-HtmlExtractionRecipe [-DataSource] <HtmlBrowserlessDataSource> [-Path] <string> [-IncludeRawContent] [-PassThru] [<CommonParameters>]
```

## DESCRIPTION
Saves a browserless extraction recipe created from a discovered data source.

## EXAMPLES

### EXAMPLE 1
```powershell
Find-HtmlDataSource -Content $html -DirectOnly | Select-Object -First 1 | Export-HtmlExtractionRecipe -Path .\recipe.json
```


## PARAMETERS

### -DataSource
Browserless data source to save as a recipe.

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
Includes raw static payloads in the recipe. Review recipe files before sharing them.

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

### -PassThru
Writes the recipe path to the pipeline.

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

### -Path
Destination JSON recipe path.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: OutFile
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `HtmlTinkerX.HtmlBrowserlessDataSource`

## OUTPUTS

- `System.String`

## RELATED LINKS

- None
