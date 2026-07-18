---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Get-HtmlExtractionProfile
## SYNOPSIS
Returns built-in extraction workflow profiles or the profile recommended by an extraction plan.

## SYNTAX
### List (Default)
```powershell
Get-HtmlExtractionProfile [[-Name] <string[]>] [-RecommendedMode <HtmlExtractionPlanMode>] [<CommonParameters>]
```

### Plan
```powershell
Get-HtmlExtractionProfile [-Plan] <HtmlExtractionPlan> [<CommonParameters>]
```

## DESCRIPTION
Returns built-in extraction workflow profiles or the profile recommended by an extraction plan.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-HtmlExtractionProfile
```


### EXAMPLE 2
```powershell
Test-HtmlExtractionPlan -Url https://example.com | Get-HtmlExtractionProfile
```


## PARAMETERS

### -Name
Optional extraction profile name filter.

```yaml
Type: String[]
Parameter Sets: List
Aliases: None
Possible values:

Required: False
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Plan
Extraction plan whose suggested profile should be returned.

```yaml
Type: HtmlExtractionPlan
Parameter Sets: Plan
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: True
```

### -RecommendedMode
Optional extraction mode filter.

```yaml
Type: Nullable`1
Parameter Sets: List
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

- `HtmlTinkerX.HtmlExtractionPlan`

## OUTPUTS

- `HtmlTinkerX.HtmlExtractionProfile`

## RELATED LINKS

- None
