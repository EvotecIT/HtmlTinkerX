---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/PSParseHTML
schema: 2.0.0
---
# Format-Css
## SYNOPSIS
Cmdlet that formats CSS content using AngleSharp.

## SYNTAX
### Content (Default)
```powershell
Format-Css -Content <string> [-OutputFile <string>] [<CommonParameters>]
```

### File
```powershell
Format-Css -Path <string> [-OutputFile <string>] [<CommonParameters>]
```

## DESCRIPTION
Cmdlet that formats CSS content using AngleSharp.

## EXAMPLES

### EXAMPLE 1
```powershell
Format-Css -Content "body{margin:0}"
```


## PARAMETERS

### -Content
CSS content to format.

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
Optional path to write the formatted CSS.

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
Path to a CSS file to format.

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
