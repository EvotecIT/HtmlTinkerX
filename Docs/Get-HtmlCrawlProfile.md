---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Get-HtmlCrawlProfile
## SYNOPSIS
Returns built-in or custom crawl profiles.

## SYNTAX
### __AllParameterSets
```powershell
Get-HtmlCrawlProfile [[-Name] <string[]>] [-Path <string>] [<CommonParameters>]
```

## DESCRIPTION
Returns built-in or custom crawl profiles.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-HtmlCrawlProfile
```


### EXAMPLE 2
```powershell
Get-HtmlCrawlProfile -Path .\crawl-profiles.json -Name custom-docs
```


## PARAMETERS

### -Name
Optional profile name filter.

```yaml
Type: String[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
Optional JSON file containing custom crawl profiles.

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

- `None`

## OUTPUTS

- `HtmlTinkerX.HtmlCrawlProfile`

## RELATED LINKS

- None
