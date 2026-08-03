---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Invoke-HtmlBrowserScript
## SYNOPSIS
Cmdlet that runs arbitrary JavaScript in an existing browser session.

## SYNTAX
### __AllParameterSets
```powershell
Invoke-HtmlBrowserScript [[-Session] <HtmlBrowserSession>] [-Script] <string> [<CommonParameters>]
```

## DESCRIPTION
Cmdlet that runs arbitrary JavaScript in an existing browser session.

## EXAMPLES

### EXAMPLE 1
```powershell
Invoke-HtmlBrowserScript -Script 'Value'
```


## PARAMETERS

### -Script
JavaScript code to execute.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Session
Browser session.

```yaml
Type: HtmlBrowserSession
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `HtmlTinkerX.HtmlBrowserSession`

## OUTPUTS

- `System.Object`

## RELATED LINKS

- None
