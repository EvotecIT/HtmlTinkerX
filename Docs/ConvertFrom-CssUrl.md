---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# ConvertFrom-CssUrl
## SYNOPSIS
Extracts URL references from CSS declarations and imports.

## SYNTAX
### Content (Default)
```powershell
ConvertFrom-CssUrl -Content <string> [-BaseUrl <uri>] [<CommonParameters>]
```

### File
```powershell
ConvertFrom-CssUrl -Path <string> [-BaseUrl <uri>] [<CommonParameters>]
```

## DESCRIPTION
Extracts URL references from CSS declarations and imports.

## EXAMPLES

### EXAMPLE 1
```powershell
ConvertFrom-CssUrl -Content '.hero { background: url(/img/hero.png); }' -BaseUrl 'https://example.org/page/'
```


## PARAMETERS

### -BaseUrl
Base URL used to resolve relative CSS URLs.

```yaml
Type: Uri
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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `System.String`

## OUTPUTS

- `HtmlTinkerX.HtmlCssUrlReference`

## RELATED LINKS

- None
