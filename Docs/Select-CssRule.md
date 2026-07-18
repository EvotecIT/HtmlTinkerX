---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Select-CssRule
## SYNOPSIS
Selects CSS style rules by selector.

## SYNTAX
### Content (Default)
```powershell
Select-CssRule -Content <string> [-Selector <string>] [-Contains] [<CommonParameters>]
```

### File
```powershell
Select-CssRule -Path <string> [-Selector <string>] [-Contains] [<CommonParameters>]
```

## DESCRIPTION
Selects CSS style rules by selector.

## EXAMPLES

### EXAMPLE 1
```powershell
Select-CssRule -Content '.btn { color: red; }' -Selector '.btn'
```


## PARAMETERS

### -Contains
Matches selector text that contains the provided selector text.

```yaml
Type: SwitchParameter
Parameter Sets: Content, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Content
CSS content to inspect.

```yaml
Type: String
Parameter Sets: Content
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue, ByPropertyName)
Accept wildcard characters: True
```

### -Path
Path to a CSS file.

```yaml
Type: String
Parameter Sets: File
Aliases: File
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Selector
Selector to match.

```yaml
Type: String
Parameter Sets: Content, File
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

- `System.String`

## OUTPUTS

- `HtmlTinkerX.HtmlCssRuleMatch`

## RELATED LINKS

- None
