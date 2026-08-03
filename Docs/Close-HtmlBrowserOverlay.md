---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Close-HtmlBrowserOverlay
## SYNOPSIS
Attempts to dismiss common cookie and modal overlays.

## SYNTAX
### __AllParameterSets
```powershell
Close-HtmlBrowserOverlay [[-Session] <HtmlBrowserSession>] [-Timeout <int>] [-InteractionDelayMs <int>] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

## DESCRIPTION
This helper tries common visible buttons and selectors such as Accept, I agree, Got it, and Close.
It is intended to remove extraction-blocking overlays in legitimate workflows, not to bypass access controls.

## EXAMPLES

### EXAMPLE 1
```powershell
$session = Start-HtmlBrowserSession -Url https://example.org/article
Close-HtmlBrowserOverlay -Session $session
Get-HtmlBrowserContent -Session $session -Selector 'main' -AsText
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

### -InteractionDelayMs
Delay after each successful dismissal.

```yaml
Type: Int32
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
Existing browser session.

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

### -Timeout
Timeout in milliseconds for each dismissal target.

```yaml
Type: Int32
Parameter Sets: __AllParameterSets
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

- `HtmlTinkerX.HtmlBrowserSession`

## OUTPUTS

- `System.String`

## RELATED LINKS

- None
