---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Format-Html
## SYNOPSIS
Cmdlet that formats HTML markup using NUglify.

## SYNTAX
### Content (Default)
```powershell
Format-Html -Content <string> [-OutputFile <string>] [-Indent <string>] [-BlockStartLine <BlockStart>] [-RemoveHTMLComments] [-RemoveOptionalTags] [-OutputTextNodesOnNewLine] [-RemoveEmptyAttributes] [-AlphabeticallyOrderAttributes] [-RemoveEmptyBlocks] [<CommonParameters>]
```

### File
```powershell
Format-Html -Path <string> [-OutputFile <string>] [-Indent <string>] [-BlockStartLine <BlockStart>] [-RemoveHTMLComments] [-RemoveOptionalTags] [-OutputTextNodesOnNewLine] [-RemoveEmptyAttributes] [-AlphabeticallyOrderAttributes] [-RemoveEmptyBlocks] [<CommonParameters>]
```

## DESCRIPTION
Cmdlet that formats HTML markup using NUglify.

## EXAMPLES

### EXAMPLE 1
```powershell
Format-Html -Content '<div>test</div>'
```


## PARAMETERS

### -AlphabeticallyOrderAttributes
Alphabetically order attributes.

```yaml
Type: SwitchParameter
Parameter Sets: Content, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -BlockStartLine
Determines how blocks start.

```yaml
Type: BlockStart
Parameter Sets: Content, File
Aliases: None
Possible values: NewLine, SameLine, UseSource

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Content
HTML content to format.

```yaml
Type: String
Parameter Sets: Content
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue, ByPropertyName)
Accept wildcard characters: True
```

### -Indent
Indentation string.

```yaml
Type: String
Parameter Sets: Content, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -OutputFile
Optional path to write the formatted HTML.

```yaml
Type: String
Parameter Sets: Content, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -OutputTextNodesOnNewLine
Output text nodes on new line.

```yaml
Type: SwitchParameter
Parameter Sets: Content, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Path
Path to an HTML file.

```yaml
Type: String
Parameter Sets: File
Aliases: File
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -RemoveEmptyAttributes
Remove empty attributes.

```yaml
Type: SwitchParameter
Parameter Sets: Content, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -RemoveEmptyBlocks
Remove empty CSS blocks.

```yaml
Type: SwitchParameter
Parameter Sets: Content, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -RemoveHTMLComments
Remove HTML comments.

```yaml
Type: SwitchParameter
Parameter Sets: Content, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -RemoveOptionalTags
Remove optional tags.

```yaml
Type: SwitchParameter
Parameter Sets: Content, File
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

- `System.String`

## OUTPUTS

- `System.String`

## RELATED LINKS

- None
