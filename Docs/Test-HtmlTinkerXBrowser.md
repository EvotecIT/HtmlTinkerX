---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Test-HtmlTinkerXBrowser
## SYNOPSIS
Checks browser cache hashes, launch readiness, lock ownership, disk space, and install history.

## SYNTAX
### __AllParameterSets
```powershell
Test-HtmlTinkerXBrowser [-Browser <HtmlBrowserEngine>] [-Repair] [<CommonParameters>]
```

## DESCRIPTION
Checks browser cache hashes, launch readiness, lock ownership, disk space, and install history.

## EXAMPLES

### EXAMPLE 1
```powershell
Test-HtmlTinkerXBrowser -Browser Chromium -Repair
```


### EXAMPLE 2
```powershell
Test-HtmlTinkerXBrowser | ConvertTo-Json -Depth 8
```


## PARAMETERS

### -Browser
One browser engine; omit to diagnose all engines.

```yaml
Type: HtmlBrowserEngine
Parameter Sets: __AllParameterSets
Aliases: None
Possible values: Chromium, Firefox, WebKit

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Repair
Download verified replacement generations for broken or missing revisions.

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `None`

## OUTPUTS

- `HtmlTinkerX.HtmlBrowserCacheReport`

## RELATED LINKS

- None
