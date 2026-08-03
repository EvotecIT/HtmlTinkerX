---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Invoke-HtmlBrowserClick
## SYNOPSIS
Clicks a selector or visible text target in a browser session before extraction.

## SYNTAX
### BySelector (Default)
```powershell
Invoke-HtmlBrowserClick [[-Session] <HtmlBrowserSession>] [-Selector] <string> [-Button <MouseButton>] [-ClickCount <int>] [-Nth <Int32>] [-Modifier <KeyboardModifier[]>] [-IfVisible] [-Timeout <int>] [-PassThru] [-OnFailureEvidence] [-FailureEvidenceFolder <string>] [<CommonParameters>]
```

### ByText
```powershell
Invoke-HtmlBrowserClick [[-Session] <HtmlBrowserSession>] [-Text] <string> [-Exact] [-Regex <string>] [-Nth <Int32>] [-IfVisible] [-Timeout <int>] [-PassThru] [-OnFailureEvidence] [-FailureEvidenceFolder <string>] [<CommonParameters>]
```

## DESCRIPTION
Clicks a selector or visible text target in a browser session before extraction.

## EXAMPLES

### EXAMPLE 1
```powershell
$session = Start-HtmlBrowserSession -Url https://example.org/app
Invoke-HtmlBrowserClick -Session $session -Selector '#loadMore'
Wait-HtmlBrowserContent -Session $session -Text 'More results' -Selector 'main'
```


### EXAMPLE 2
```powershell
$session = Start-HtmlBrowserSession -Url https://example.org/app
Invoke-HtmlBrowserClick -Session $session -Text 'Accept' -IfVisible
```


## PARAMETERS

### -Button
Mouse button to use.

```yaml
Type: MouseButton
Parameter Sets: BySelector
Aliases: None
Possible values: Left, Right, Middle

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ClickCount
Number of clicks.

```yaml
Type: Int32
Parameter Sets: BySelector
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Exact
Use exact text match.

```yaml
Type: SwitchParameter
Parameter Sets: ByText
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -FailureEvidenceFolder
Root folder where failure evidence is written when OnFailureEvidence is used.

```yaml
Type: String
Parameter Sets: BySelector, ByText
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IfVisible
Return without error when the target is absent, hidden, or times out.

```yaml
Type: SwitchParameter
Parameter Sets: BySelector, ByText
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Modifier
Keyboard modifiers.

```yaml
Type: KeyboardModifier[]
Parameter Sets: BySelector
Aliases: None
Possible values: Alt, Control, ControlOrMeta, Meta, Shift

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Nth
Zero-based index of the matching selector or text target to click.

```yaml
Type: Int32
Parameter Sets: BySelector, ByText
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -OnFailureEvidence
Export screenshots, HTML, text, Markdown, network summary, and failure context if the click fails.

```yaml
Type: SwitchParameter
Parameter Sets: BySelector, ByText
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PassThru
Return the session object.

```yaml
Type: SwitchParameter
Parameter Sets: BySelector, ByText
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Regex
Regular expression for text match.

```yaml
Type: String
Parameter Sets: ByText
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Selector
CSS selector of the element to click.

```yaml
Type: String
Parameter Sets: BySelector
Aliases: None
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Session
Existing browser session.

```yaml
Type: HtmlBrowserSession
Parameter Sets: BySelector, ByText
Aliases: None
Possible values:

Required: False
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Text
Visible text of the element to click.

```yaml
Type: String
Parameter Sets: ByText
Aliases: None
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Timeout
Timeout in milliseconds.

```yaml
Type: Int32
Parameter Sets: BySelector, ByText
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

- `HtmlTinkerX.HtmlBrowserSession`

## RELATED LINKS

- None
