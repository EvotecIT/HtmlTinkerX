---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Get-HtmlElementFingerprint
## SYNOPSIS
Records the structural identity of elements so they can be found again after a page layout change.

## SYNTAX
### Content (Default)
```powershell
Get-HtmlElementFingerprint [-Content] <string> [-Selector] <string> [<CommonParameters>]
```

### Element
```powershell
Get-HtmlElementFingerprint [-Element] <IElement> [<CommonParameters>]
```

### File
```powershell
Get-HtmlElementFingerprint [-Path] <string> [-Selector] <string> [<CommonParameters>]
```

## DESCRIPTION
Records the structural identity of elements so they can be found again after a page layout change.

## EXAMPLES

### EXAMPLE 1
```powershell
$fingerprint = Get-HtmlElementFingerprint -Path .\release-notes.html -Selector 'div.release-entry' | Select-Object -First 1
```


### EXAMPLE 2
```powershell
Select-HtmlElement -Content $html -Selector 'main h1' -First | Get-HtmlElementFingerprint
```


## PARAMETERS

### -Content
HTML content containing the elements.

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

### -Element
Element to fingerprint, for example from Select-HtmlElement. Its position in the parsed document is recorded.

```yaml
Type: IElement
Parameter Sets: Element
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Path
Path to an HTML file containing the elements.

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
CSS selector whose matches are fingerprinted, one fingerprint per match in document order.

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

- `AngleSharp.Dom.IElement`

## OUTPUTS

- `HtmlTinkerX.HtmlElementFingerprint`

## RELATED LINKS

- None
