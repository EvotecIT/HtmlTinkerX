---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Optimize-Css
## SYNOPSIS
Cmdlet that minifies CSS content.

## SYNTAX
### Css (Default)
```powershell
Optimize-Css -Css <string> [-OutputFile <string>] [<CommonParameters>]
```

### Path
```powershell
Optimize-Css -Path <string> [-OutputFile <string>] [<CommonParameters>]
```

## DESCRIPTION
Cmdlet that minifies CSS content.

## EXAMPLES

### EXAMPLE 1
```powershell
Optimize-Css -Css $css
```


## PARAMETERS

### -Css
CSS content to optimize.

```yaml
Type: String
Parameter Sets: Css
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -OutputFile
Optional output file for the optimized CSS.

```yaml
Type: String
Parameter Sets: Css, Path
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
Parameter Sets: Path
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

- `System.String`

## RELATED LINKS

- None
