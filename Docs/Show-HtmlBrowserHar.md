---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Show-HtmlBrowserHar
## SYNOPSIS
Generates a simple HTML viewer for a HAR file and returns the parsed data.

## SYNTAX
### __AllParameterSets
```powershell
Show-HtmlBrowserHar [-Path] <string> [-OutFile <string>] [-Open] [<CommonParameters>]
```

## DESCRIPTION
Generates a simple HTML viewer for a HAR file and returns the parsed data.

## EXAMPLES

### EXAMPLE 1
```powershell
Show-HtmlBrowserHar -Path 'C:\Path'
```


## PARAMETERS

### -Open
Open the generated HTML viewer.

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

### -OutFile
Optional output HTML file path.

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

### -Path
Path to the HAR file.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: File
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

- `System.Object`

## RELATED LINKS

- None
