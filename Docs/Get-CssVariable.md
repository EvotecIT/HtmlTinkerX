---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Get-CssVariable
## SYNOPSIS
Returns CSS custom property declarations.

## SYNTAX
### Content (Default)
```powershell
Get-CssVariable -Content <string> [-Name <string>] [-Contains] [<CommonParameters>]
```

### File
```powershell
Get-CssVariable -Path <string> [-Name <string>] [-Contains] [<CommonParameters>]
```

## DESCRIPTION
Returns CSS custom property declarations.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-CssVariable -Content ':root { --brand-color: #0369a1; }'
```


## PARAMETERS

### -Contains
Matches custom property names that contain the provided name.

```yaml
Type: SwitchParameter
Parameter Sets: Content, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
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
Accept wildcard characters: False
```

### -Name
Custom property name to match, such as --brand-color.

```yaml
Type: String
Parameter Sets: Content, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
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
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `System.String`

## OUTPUTS

- `HtmlTinkerX.HtmlCssVariableMatch`

## RELATED LINKS

- None
