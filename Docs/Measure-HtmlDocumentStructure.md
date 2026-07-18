---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Measure-HtmlDocumentStructure
## SYNOPSIS
Returns basic statistics for an HTML document.

## SYNTAX
### Content (Default)
```powershell
Measure-HtmlDocumentStructure -Content <string> [<CommonParameters>]
```

### File
```powershell
Measure-HtmlDocumentStructure -Path <string> [<CommonParameters>]
```

## DESCRIPTION
Returns basic statistics for an HTML document.

## EXAMPLES

### EXAMPLE 1
```powershell
Measure-HTMLDocument -Content "<html>...</html>"
```


### EXAMPLE 2
```powershell
Measure-HTMLDocument -Path ./page.html
```


## PARAMETERS

### -Content
HTML string to analyze.

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
Path to a local HTML file.

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

- `HtmlTinkerX.HtmlDocumentStatistics`

## RELATED LINKS

- None
