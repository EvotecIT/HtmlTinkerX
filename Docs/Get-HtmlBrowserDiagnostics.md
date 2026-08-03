---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Get-HtmlBrowserDiagnostics
## SYNOPSIS
Returns browser runtime, storage, console, and observed network diagnostics for a session.

## SYNTAX
### __AllParameterSets
```powershell
Get-HtmlBrowserDiagnostics [[-Session] <HtmlBrowserSession>] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

## DESCRIPTION
Use this command to understand extraction reliability signals such as viewport, locale, storage keys,
failed requests, console errors, Fetch/XHR calls, and WebSocket activity. It reports diagnostics only;
it does not attempt to hide automation or bypass site protections.

## EXAMPLES

### EXAMPLE 1
```powershell
$session = Start-HtmlBrowserSession -Url https://example.org/app
Wait-HtmlBrowserContent -Session $session -Stable
$diagnostics = Get-HtmlBrowserDiagnostics -Session $session
$diagnostics.ObservedApiCalls
$diagnostics.ConsistencyWarnings
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
Browser session to inspect.

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

- `HtmlTinkerX.HtmlBrowserDiagnostics`

## RELATED LINKS

- None
