---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Close-HtmlBrowserSession
## SYNOPSIS
Cmdlet that disposes an existing browser session.

## SYNTAX
### __AllParameterSets
```powershell
Close-HtmlBrowserSession [-Session] <HtmlBrowserSession> [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

## DESCRIPTION
Cmdlet that disposes an existing browser session.

## EXAMPLES

### EXAMPLE 1
```powershell
Close-HTMLSession -Session $session
```


## PARAMETERS

### -CancellationToken
Token used to cancel the operation.

```yaml
Type: CancellationToken
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Session
Browser session to dispose.

```yaml
Type: HtmlBrowserSession
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
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

- `None`

## RELATED LINKS

- None
