---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Import-HtmlExtractionRecipe
## SYNOPSIS
Loads a browserless extraction recipe from disk.

## SYNTAX
### __AllParameterSets
```powershell
Import-HtmlExtractionRecipe [-Path] <string> [<CommonParameters>]
```

## DESCRIPTION
Loads a browserless extraction recipe from disk.

## EXAMPLES

### EXAMPLE 1
```powershell
Import-HtmlExtractionRecipe -Path .\recipe.json
```


## PARAMETERS

### -Path
Recipe JSON path.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: File
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `None`

## OUTPUTS

- `HtmlTinkerX.HtmlBrowserlessExtractionRecipe`

## RELATED LINKS

- None
