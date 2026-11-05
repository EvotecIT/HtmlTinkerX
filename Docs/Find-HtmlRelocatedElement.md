---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Find-HtmlRelocatedElement
## SYNOPSIS
Suggests where content moved when a saved CSS selector stops matching after a page layout change.

## SYNTAX
### Content (Default)
```powershell
Find-HtmlRelocatedElement [-Content] <string> [-Selector] <string> -Fingerprint <HtmlElementFingerprint> [-Limit <int>] [<CommonParameters>]
```

### File
```powershell
Find-HtmlRelocatedElement [-Path] <string> [-Selector] <string> -Fingerprint <HtmlElementFingerprint> [-Limit <int>] [<CommonParameters>]
```

## DESCRIPTION
Suggests where content moved when a saved CSS selector stops matching after a page layout change.

## EXAMPLES

### EXAMPLE 1
```powershell
Find-HtmlRelocatedElement -Path .\release-notes.html -Selector 'div.release-entry' -Fingerprint $fingerprint
```


### EXAMPLE 2
```powershell
$candidate = Find-HtmlRelocatedElement -Content $html -Selector $selector -Fingerprint $fingerprint | Where-Object Confidence -eq 'High' | Select-Object -First 1
```


## PARAMETERS

### -Content
Current HTML content to search.

```yaml
Type: String
Parameter Sets: Content
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Fingerprint
Fingerprint captured while the selector still matched.

```yaml
Type: HtmlElementFingerprint
Parameter Sets: Content, File
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Limit
Maximum number of ranked candidates to return.

```yaml
Type: Int32
Parameter Sets: Content, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
Path to the current HTML file to search.

```yaml
Type: String
Parameter Sets: File
Aliases: File
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Selector
Saved selector that no longer finds the expected content. A candidate with the same selector is omitted.

```yaml
Type: String
Parameter Sets: Content, File
Aliases: None
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `HtmlTinkerX.HtmlElementFingerprint`

## OUTPUTS

- `HtmlTinkerX.HtmlSelectorRelocationCandidate`

## RELATED LINKS

- None
