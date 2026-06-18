---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/PSParseHTML
schema: 2.0.0
---
# Wait-HtmlBrowserContent
## SYNOPSIS
Waits for browser-rendered text or DOM stability.

## SYNTAX
### Text (Default)
```powershell
Wait-HtmlBrowserContent [[-Session] <HtmlBrowserSession>] [-Text] <string> [-Selector <string>] [-Exact] [-Timeout <int>] [-PassThru] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

### Element
```powershell
Wait-HtmlBrowserContent [[-Session] <HtmlBrowserSession>] -Selector <string> -Element [-Visible] [-Hidden] [-Enabled] [-Disabled] [-InViewport] [-Timeout <int>] [-PassThru] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

### Stable
```powershell
Wait-HtmlBrowserContent [[-Session] <HtmlBrowserSession>] -Stable [-StableMilliseconds <int>] [-PollMilliseconds <int>] [-Timeout <int>] [-PassThru] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

## DESCRIPTION
Waits for browser-rendered text or DOM stability.

## EXAMPLES

### EXAMPLE 1
```powershell
$session = Start-HtmlBrowserSession -Url https://example.org/search
Wait-HtmlBrowserContent -Session $session -Text 'Results' -Selector 'main'
```


### EXAMPLE 2
```powershell
$session = Start-HtmlBrowserSession -Url https://example.org/app
Wait-HtmlBrowserContent -Session $session -Stable -StableMilliseconds 500
```


## PARAMETERS

### -CancellationToken
Token used to cancel the operation.

```yaml
Type: CancellationToken
Parameter Sets: Text, Element, Stable
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Disabled
Wait until the element is disabled.

```yaml
Type: SwitchParameter
Parameter Sets: Element
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Element
Wait for an element state instead of text.

```yaml
Type: SwitchParameter
Parameter Sets: Element
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Enabled
Wait until the element is enabled.

```yaml
Type: SwitchParameter
Parameter Sets: Element
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Exact
Use exact text match.

```yaml
Type: SwitchParameter
Parameter Sets: Text
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Hidden
Wait until the element is hidden or absent.

```yaml
Type: SwitchParameter
Parameter Sets: Element
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -InViewport
Wait until the element is inside the viewport.

```yaml
Type: SwitchParameter
Parameter Sets: Element
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -PassThru
Return the session object.

```yaml
Type: SwitchParameter
Parameter Sets: Text, Element, Stable
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -PollMilliseconds
Polling interval in milliseconds for stability checks.

```yaml
Type: Int32
Parameter Sets: Stable
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Selector
Selector scope used when waiting for text.

```yaml
Type: String
Parameter Sets: Text, Element
Aliases: None
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
Parameter Sets: Text, Element, Stable
Aliases: None
Possible values:

Required: False
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: True
```

### -Stable
Wait until the document HTML is stable.

```yaml
Type: SwitchParameter
Parameter Sets: Stable
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -StableMilliseconds
Stable interval in milliseconds.

```yaml
Type: Int32
Parameter Sets: Stable
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Text
Text to wait for.

```yaml
Type: String
Parameter Sets: Text
Aliases: None
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Timeout
Timeout in milliseconds.

```yaml
Type: Int32
Parameter Sets: Text, Element, Stable
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Visible
Wait until the element is visible.

```yaml
Type: SwitchParameter
Parameter Sets: Element
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

- `HtmlTinkerX.HtmlBrowserSession`

## RELATED LINKS

- None
