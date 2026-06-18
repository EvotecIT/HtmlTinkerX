---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/PSParseHTML
schema: 2.0.0
---
# Select-HtmlNode
## SYNOPSIS
Selects HtmlAgilityPack nodes using XPath or common tag and attribute predicates.

## SYNTAX
### XPath (Default)
```powershell
Select-HtmlNode [-InputObject] <Object> [-XPath] <string> [-Single] [<CommonParameters>]
```

### Predicate
```powershell
Select-HtmlNode [-InputObject] <Object> -Tag <string> [-AttributeName <string>] [-AttributeValue <string>] [-Contains] [-StartsWith] [-Text] [-Single] [<CommonParameters>]
```

## DESCRIPTION
Selects HtmlAgilityPack nodes using XPath or common tag and attribute predicates.

## EXAMPLES

### EXAMPLE 1
```powershell
ConvertFrom-Html -Content $html | Select-HtmlNode -XPath '//a'
```


### EXAMPLE 2
```powershell
ConvertFrom-Html -Content $html | Select-HtmlNode -Tag h3 -AttributeName class -AttributeValue 'title' -Single
```


## PARAMETERS

### -AttributeName
Attribute name used to build a simple XPath predicate.

```yaml
Type: String
Parameter Sets: Predicate
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -AttributeValue
Attribute value used to build a simple XPath predicate.

```yaml
Type: String
Parameter Sets: Predicate
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Contains
Builds a contains() predicate for attribute matching.

```yaml
Type: SwitchParameter
Parameter Sets: Predicate
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -InputObject
HtmlAgilityPack node, document, or raw HTML content to search.

```yaml
Type: Object
Parameter Sets: XPath, Predicate
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: True
```

### -Single
Returns only the first matching node.

```yaml
Type: SwitchParameter
Parameter Sets: XPath, Predicate
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -StartsWith
Builds a starts-with() predicate for attribute matching.

```yaml
Type: SwitchParameter
Parameter Sets: Predicate
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Tag
Tag name used to build a simple XPath expression.

```yaml
Type: String
Parameter Sets: Predicate
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Text
Returns text nodes beneath the matching elements.

```yaml
Type: SwitchParameter
Parameter Sets: Predicate
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -XPath
XPath expression passed to HtmlNode.SelectNodes or HtmlNode.SelectSingleNode.

```yaml
Type: String
Parameter Sets: XPath
Aliases: None
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

- `System.Object`

## OUTPUTS

- `HtmlAgilityPack.HtmlNode`

## RELATED LINKS

- None
