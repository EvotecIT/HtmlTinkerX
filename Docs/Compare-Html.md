---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/PSParseHTML
schema: 2.0.0
---
# Compare-Html
## SYNOPSIS
Cmdlet that compares HTML content and returns differences.

## SYNTAX
### __AllParameterSets
```powershell
Compare-Html [-Reference] <string> [-Difference] <string> [<CommonParameters>]
```

## DESCRIPTION
Cmdlet that compares HTML content and returns differences.

## EXAMPLES

### EXAMPLE 1
```powershell
Compare-Html -Reference $file1 -Difference $file2
```


## PARAMETERS

### -Difference
HTML to compare against the reference.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Reference
Reference HTML markup, file path or URL.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
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

- `AngleSharp.Diffing.Core.IDiff`

## RELATED LINKS

- None
