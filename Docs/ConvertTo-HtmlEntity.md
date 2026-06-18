---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/PSParseHTML
schema: 2.0.0
---
# ConvertTo-HtmlEntity
## SYNOPSIS
Encodes text as HTML entities.

## SYNTAX
### __AllParameterSets
```powershell
ConvertTo-HtmlEntity [-Text] <string> [<CommonParameters>]
```

## DESCRIPTION
Encodes text as HTML entities.

## EXAMPLES

### EXAMPLE 1
```powershell
ConvertTo-HtmlEntity -Text 'A&B'
```


## PARAMETERS

### -Text
Text to encode as HTML entities.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: Content, InputObject
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue, ByPropertyName)
Accept wildcard characters: True
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `System.String`

## OUTPUTS

- `System.String`

## RELATED LINKS

- None
