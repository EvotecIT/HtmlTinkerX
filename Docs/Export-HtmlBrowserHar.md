---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Export-HtmlBrowserHar
## SYNOPSIS
Saves network traffic from a browser session to a HAR file.

## SYNTAX
### Session
```powershell
Export-HtmlBrowserHar [[-Session] <HtmlBrowserSession>] -OutFile <string> [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

### Har
```powershell
Export-HtmlBrowserHar [[-Har] <Har>] -OutFile <string> [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

## DESCRIPTION
Saves network traffic from a browser session to a HAR file.

## EXAMPLES

### EXAMPLE 1
```powershell
Export-HtmlBrowserHar -OutFile 'Value'
```


## PARAMETERS

### -CancellationToken
Optional cancellation token.

```yaml
Type: CancellationToken
Parameter Sets: Session, Har
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Har
HAR object to write.

```yaml
Type: Har
Parameter Sets: Har
Aliases: None
Possible values:

Required: False
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -OutFile
Destination HAR file path.

```yaml
Type: String
Parameter Sets: Session, Har
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Session
Browser session to export.

```yaml
Type: HtmlBrowserSession
Parameter Sets: Session
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
- `HtmlTinkerX.Har`

## OUTPUTS

- `None`

## RELATED LINKS

- None
