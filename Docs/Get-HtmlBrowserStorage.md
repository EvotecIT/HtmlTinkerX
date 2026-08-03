---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Get-HtmlBrowserStorage
## SYNOPSIS
Gets localStorage and sessionStorage entries from an active browser session.

## SYNTAX
### __AllParameterSets
```powershell
Get-HtmlBrowserStorage [[-Session] <HtmlBrowserSession>] [-Scope <string>] [-Key <string>] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

## DESCRIPTION
Gets localStorage and sessionStorage entries from an active browser session.

## EXAMPLES

### EXAMPLE 1
```powershell
$session = Start-HtmlBrowserSession -Url https://example.org/app
Get-HtmlBrowserStorage -Session $session -Scope All
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

### -Key
Optional storage key to read.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Scope
Storage scope to read.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values: All, Local, Session

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `HtmlTinkerX.HtmlBrowserSession`

## OUTPUTS

- `HtmlTinkerX.HtmlBrowserStorageItem`

## RELATED LINKS

- None
