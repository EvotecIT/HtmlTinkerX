---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/PSParseHTML
schema: 2.0.0
---
# Select-HtmlAttributeValue
## SYNOPSIS
Returns an HTML attribute value from HtmlAgilityPack nodes, attributes, or matching object properties.

## SYNTAX
### __AllParameterSets
```powershell
Select-HtmlAttributeValue [-InputObject] <Object> [[-AttributeName] <string>] [-DefaultValue <string>] [-TreatEmptyAsMissing] [<CommonParameters>]
```

## DESCRIPTION
Returns an HTML attribute value from HtmlAgilityPack nodes, attributes, or matching object properties.

## EXAMPLES

### EXAMPLE 1
```powershell
ConvertFrom-Html -Content $html | Select-HtmlNode -XPath '//a' | Select-HtmlAttributeValue -AttributeName href
```


## PARAMETERS

### -AttributeName
Attribute or property name to read.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -DefaultValue
Value returned when the requested attribute is missing.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -InputObject
HtmlAgilityPack node, attribute, document, or an object with a matching property.

```yaml
Type: Object
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: True
```

### -TreatEmptyAsMissing
Returns DefaultValue when the attribute exists but its value is empty.

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `System.Object`

## OUTPUTS

- `System.String`

## RELATED LINKS

- None
