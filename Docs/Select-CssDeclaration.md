---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Select-CssDeclaration
## SYNOPSIS
Selects CSS declarations by property and optional selector.

## SYNTAX
### Content (Default)
```powershell
Select-CssDeclaration -Content <string> [-Property <string>] [-Selector <string>] [-Contains] [<CommonParameters>]
```

### File
```powershell
Select-CssDeclaration -Path <string> [-Property <string>] [-Selector <string>] [-Contains] [<CommonParameters>]
```

## DESCRIPTION
Selects CSS declarations by property and optional selector.

## EXAMPLES

### EXAMPLE 1
```powershell
Select-CssDeclaration -Content '.btn { color: red; margin: 0; }' -Property color
```


## PARAMETERS

### -Contains
Matches property or selector text that contains the provided value.

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

### -Property
CSS property to match.

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

### -Selector
Optional selector to match.

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `System.String`

## OUTPUTS

- `HtmlTinkerX.HtmlCssDeclarationMatch`

## RELATED LINKS

- None
