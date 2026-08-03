---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Select-HtmlInnerText
## SYNOPSIS
Returns inner text from AngleSharp or HtmlAgilityPack elements and documents.

## SYNTAX
### __AllParameterSets
```powershell
Select-HtmlInnerText [-InputObject] <Object> [-DeEntitize] [-NoTrim] [-DefaultValue <string>] [<CommonParameters>]
```

## DESCRIPTION
Returns inner text from AngleSharp or HtmlAgilityPack elements and documents.

## EXAMPLES

### EXAMPLE 1
```powershell
ConvertFrom-Html -Content $html | Select-HtmlNode -XPath '//p' | Select-HtmlInnerText -DeEntitize
```


## PARAMETERS

### -DeEntitize
Decodes HTML entities in the returned text.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: DecodeEntities
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DefaultValue
Value returned when the extracted text is empty.

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

### -InputObject
AngleSharp or HtmlAgilityPack element, document, attribute, or raw string content.

```yaml
Type: Object
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -NoTrim
Preserves leading and trailing whitespace.

```yaml
Type: SwitchParameter
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

- `System.Object`

## OUTPUTS

- `System.String`

## RELATED LINKS

- None
