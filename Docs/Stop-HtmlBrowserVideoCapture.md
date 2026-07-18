---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Stop-HtmlBrowserVideoCapture
## SYNOPSIS
Cmdlet that stops video recording for a browser session.

## SYNTAX
### __AllParameterSets
```powershell
Stop-HtmlBrowserVideoCapture [[-Session] <HtmlBrowserSession>] [-OutFile <string>] [<CommonParameters>]
```

## DESCRIPTION
Cmdlet that stops video recording for a browser session.

## EXAMPLES

### EXAMPLE 1
```powershell
Stop-HtmlBrowserVideoCapture -OutFile 'Value'
```


## PARAMETERS

### -OutFile
Optional path to save the recorded video.

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

### -Session
Browser session with an active recording.

```yaml
Type: HtmlBrowserSession
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: True
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `HtmlTinkerX.HtmlBrowserSession`

## OUTPUTS

- `System.Object`

## RELATED LINKS

- None
