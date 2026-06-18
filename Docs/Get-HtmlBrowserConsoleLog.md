---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/PSParseHTML
schema: 2.0.0
---
# Get-HtmlBrowserConsoleLog
## SYNOPSIS
Cmdlet that retrieves captured console log entries from a session.

## SYNTAX
### __AllParameterSets
```powershell
Get-HtmlBrowserConsoleLog [[-Session] <HtmlBrowserSession>] [[-Severity] <HtmlConsoleSeverity>] [<CommonParameters>]
```

## DESCRIPTION
Cmdlet that retrieves captured console log entries from a session.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-HTMLConsoleLog -Session $session
```


## PARAMETERS

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
Accept wildcard characters: True
```

### -Severity
Optional severity filter.

```yaml
Type: Nullable`1
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `HtmlTinkerX.HtmlBrowserSession`

## OUTPUTS

- `HtmlTinkerX.HtmlConsoleEntry`

## RELATED LINKS

- None
