---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/PSParseHTML
schema: 2.0.0
---
# Optimize-JavaScript
## SYNOPSIS
Cmdlet that minifies JavaScript code.

## SYNTAX
### Content (Default)
```powershell
Optimize-JavaScript -Content <string> [-OutputFile <string>] [<CommonParameters>]
```

### File
```powershell
Optimize-JavaScript -Path <string> [-OutputFile <string>] [<CommonParameters>]
```

## DESCRIPTION
Cmdlet that minifies JavaScript code.

## EXAMPLES

### EXAMPLE 1
```powershell
Optimize-JavaScript -Content $js
```


## PARAMETERS

### -Content
JavaScript content to optimize.

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

### -OutputFile
Optional file path to write optimized output.

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

### -Path
Path to a JavaScript file to optimize.

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `System.String`

## OUTPUTS

- `System.String`

## RELATED LINKS

- None
