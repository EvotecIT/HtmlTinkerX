---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Export-HtmlBrowserProfile
## SYNOPSIS
Exports a reusable browser profile to JSON.

## SYNTAX
### __AllParameterSets
```powershell
Export-HtmlBrowserProfile [-Profile] <HtmlBrowserProfile> [-Path] <string> [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

## DESCRIPTION
Exports a reusable browser profile to JSON.

## EXAMPLES

### EXAMPLE 1
```powershell
Export-HtmlBrowserProfile -Path 'C:\Path'
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

### -Path
Path to the browser profile JSON file.

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

### -Profile
Browser profile to export.

```yaml
Type: HtmlBrowserProfile
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

- `HtmlTinkerX.HtmlBrowserProfile`

## OUTPUTS

- `None`

## RELATED LINKS

- None
