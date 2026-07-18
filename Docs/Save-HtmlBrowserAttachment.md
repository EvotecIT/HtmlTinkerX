---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Save-HtmlBrowserAttachment
## SYNOPSIS
Cmdlet that saves files downloaded while rendering a web page.

## SYNTAX
### Default (Default)
```powershell
Save-HtmlBrowserAttachment [-Url] <string> -Path <string> [-Browser <HtmlBrowserEngine>] [-Clean] [-Visible] [-SlowMo <int>] [-Filter <string>] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

### Session
```powershell
Save-HtmlBrowserAttachment [[-Session] <HtmlBrowserSession>] -Path <string> [-SlowMo <int>] [-Filter <string>] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

## DESCRIPTION
Cmdlet that saves files downloaded while rendering a web page.

## EXAMPLES

### EXAMPLE 1
```powershell
Save-HTMLAttachment -Url https://example.com/download.html -Path C:\temp
```


## PARAMETERS

### -Browser
Browser engine to use for rendering.

```yaml
Type: HtmlBrowserEngine
Parameter Sets: Default
Aliases: None
Possible values: Chromium, Firefox, WebKit

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -CancellationToken
Token used to cancel the operation.

```yaml
Type: CancellationToken
Parameter Sets: Default, Session
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Clean
Force re-download of browser runtimes.

```yaml
Type: SwitchParameter
Parameter Sets: Default
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Filter
Optional filter applied to download URLs or file names.

```yaml
Type: String
Parameter Sets: Default, Session
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Path
Directory where downloads will be saved.

```yaml
Type: String
Parameter Sets: Default, Session
Aliases: File
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Session
Existing browser session.

```yaml
Type: HtmlBrowserSession
Parameter Sets: Session
Aliases: None
Possible values:

Required: False
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: True
```

### -SlowMo
Slow down Playwright actions by the specified milliseconds.

```yaml
Type: Int32
Parameter Sets: Default, Session
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Url
URL of the web page.

```yaml
Type: String
Parameter Sets: Default
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Visible
Show the browser instead of running headless.

```yaml
Type: SwitchParameter
Parameter Sets: Default
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `HtmlTinkerX.HtmlBrowserSession`

## OUTPUTS

- `System.String`

## RELATED LINKS

- None
