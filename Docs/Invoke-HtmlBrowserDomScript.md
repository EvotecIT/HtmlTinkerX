---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Invoke-HtmlBrowserDomScript
## SYNOPSIS
Cmdlet that executes JavaScript against HTML using AngleSharp.Js.

## SYNTAX
### Content (Default)
```powershell
Invoke-HtmlBrowserDomScript -Content <string> -Script <string> [<CommonParameters>]
```

### Path
```powershell
Invoke-HtmlBrowserDomScript -Path <string> -Script <string> [<CommonParameters>]
```

## DESCRIPTION
Cmdlet that executes JavaScript against HTML using AngleSharp.Js.

## EXAMPLES

### EXAMPLE 1
```powershell
Invoke-HtmlBrowserDomScript -Content 'Value' -Script 'Value'
```


### EXAMPLE 2
```powershell
Invoke-HtmlBrowserDomScript -Path 'C:\Path' -Script 'Value'
```


## PARAMETERS

### -Content
HTML content to process.

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
Path to a HTML file.

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

### -Script
JavaScript code to run.

```yaml
Type: String
Parameter Sets: Content, Path
Aliases: None
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

- `System.Object`

## RELATED LINKS

- None
