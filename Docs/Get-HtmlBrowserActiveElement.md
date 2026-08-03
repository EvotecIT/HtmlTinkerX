---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Get-HtmlBrowserActiveElement
## SYNOPSIS
Returns browser-observed information about the active focused element.

## SYNTAX
### __AllParameterSets
```powershell
Get-HtmlBrowserActiveElement [[-Session] <HtmlBrowserSession>] [-IncludeAttributes] [-IncludeHtml] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

## DESCRIPTION
Returns browser-observed information about the active focused element.

## EXAMPLES

### EXAMPLE 1
```powershell
$session = Start-HtmlBrowserSession -Url https://example.org/search
Invoke-HtmlBrowserClick -Session $session -Selector 'input[type=search]'
Get-HtmlBrowserActiveElement -Session $session -IncludeAttributes
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

### -IncludeAttributes
Include all element attributes.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeHtml
Include inner and outer HTML.

```yaml
Type: SwitchParameter
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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `HtmlTinkerX.HtmlBrowserSession`

## OUTPUTS

- `HtmlTinkerX.HtmlBrowserElementInfo`

## RELATED LINKS

- None
