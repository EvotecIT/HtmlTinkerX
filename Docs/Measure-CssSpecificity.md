---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/PSParseHTML
schema: 2.0.0
---
# Measure-CssSpecificity
## SYNOPSIS
Measures CSS selector specificity.

## SYNTAX
### __AllParameterSets
```powershell
Measure-CssSpecificity [-Selector] <string[]> [<CommonParameters>]
```

## DESCRIPTION
Measures CSS selector specificity.

## EXAMPLES

### EXAMPLE 1
```powershell
'.btn', '#app .btn:hover' | Measure-CssSpecificity
```


## PARAMETERS

### -Selector
Selectors to measure.

```yaml
Type: String[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: True
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `System.String[]`

## OUTPUTS

- `HtmlTinkerX.HtmlCssSpecificity`

## RELATED LINKS

- None
