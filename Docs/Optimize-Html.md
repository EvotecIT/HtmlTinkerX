---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Optimize-Html
## SYNOPSIS
Cmdlet that minifies HTML content using NUglify.

## SYNTAX
### Content (Default)
```powershell
Optimize-Html -Content <string> [-OutputFile <string>] [-CSSDecodeEscapes] [-TreatAsDocument] [-RemoveComments] [-RemoveOptionalTags] [-ShortBooleanAttributes] [-HtmlSettings <HtmlSettings>] [<CommonParameters>]
```

### File
```powershell
Optimize-Html -Path <string> [-OutputFile <string>] [-CSSDecodeEscapes] [-TreatAsDocument] [-RemoveComments] [-RemoveOptionalTags] [-ShortBooleanAttributes] [-HtmlSettings <HtmlSettings>] [<CommonParameters>]
```

## DESCRIPTION
Cmdlet that minifies HTML content using NUglify.

## EXAMPLES

### EXAMPLE 1
```powershell
Optimize-Html -Content $html
```


## PARAMETERS

### -Content
HTML content to optimize.

```yaml
Type: String
Parameter Sets: Content
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -CSSDecodeEscapes
Decode CSS escape sequences while minifying.

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

### -HtmlSettings
Custom NUglify settings to use during optimization.

```yaml
Type: HtmlSettings
Parameter Sets: Content, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -OutputFile
Optional path to write the optimized HTML.

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
Path to a HTML file.

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

### -RemoveComments
Remove HTML comments during optimization.

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

### -RemoveOptionalTags
Remove optional HTML tags such as closing </p>.

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

### -ShortBooleanAttributes
Shorten boolean attributes during optimization.

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

### -TreatAsDocument
Treat the input as a full HTML document.

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `System.String`

## OUTPUTS

- `System.String`

## RELATED LINKS

- None
