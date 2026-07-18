---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Get-HtmlBrowserNetworkLog
## SYNOPSIS
Cmdlet that retrieves captured network log entries from a session.

## SYNTAX
### __AllParameterSets
```powershell
Get-HtmlBrowserNetworkLog [[-Session] <HtmlBrowserSession>] [<CommonParameters>]
```

## DESCRIPTION
Cmdlet that retrieves captured network log entries from a session.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-HtmlBrowserNetworkLog -Session 'Value'
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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `HtmlTinkerX.HtmlBrowserSession`

## OUTPUTS

- `HtmlTinkerX.HtmlNetworkEntry`

## RELATED LINKS

- None
