---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Format-JavaScript
## SYNOPSIS
Cmdlet that formats JavaScript code using JsBeautifier.

## SYNTAX
### Content (Default)
```powershell
Format-JavaScript -Content <string> [-OutputFile <string>] [-IndentSize <uint>] [-IndentChar <char>] [-IndentWithTabs <bool>] [-PreserveNewlines <bool>] [-MaxPreserveNewlines <float>] [-JslintHappy <bool>] [-BraceStyle <BraceStyle>] [-KeepArrayIndentation <bool>] [-KeepFunctionIndentation <bool>] [-EvalCode <bool>] [-WrapLineLength <int>] [-SplitLongStringLiterals] [-MaxStringLiteralLength <int>] [-BreakChainedMethods <bool>] [<CommonParameters>]
```

### File
```powershell
Format-JavaScript -Path <string> [-OutputFile <string>] [-IndentSize <uint>] [-IndentChar <char>] [-IndentWithTabs <bool>] [-PreserveNewlines <bool>] [-MaxPreserveNewlines <float>] [-JslintHappy <bool>] [-BraceStyle <BraceStyle>] [-KeepArrayIndentation <bool>] [-KeepFunctionIndentation <bool>] [-EvalCode <bool>] [-WrapLineLength <int>] [-SplitLongStringLiterals] [-MaxStringLiteralLength <int>] [-BreakChainedMethods <bool>] [<CommonParameters>]
```

## DESCRIPTION
Cmdlet that formats JavaScript code using JsBeautifier.

## EXAMPLES

### EXAMPLE 1
```powershell
Format-JavaScript -Path script.js
```


## PARAMETERS

### -BraceStyle
Brace formatting style.

```yaml
Type: BraceStyle
Parameter Sets: Content, File
Aliases: None
Possible values: Expand, Collapse, EndExpand

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -BreakChainedMethods
Break chained methods.

```yaml
Type: Boolean
Parameter Sets: Content, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Content
JavaScript code to format.

```yaml
Type: String
Parameter Sets: Content
Aliases: FileContent
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue, ByPropertyName)
Accept wildcard characters: True
```

### -EvalCode
Preserve eval code.

```yaml
Type: Boolean
Parameter Sets: Content, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -IndentChar
Indentation character.

```yaml
Type: Char
Parameter Sets: Content, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -IndentSize
Number of spaces for indentation.

```yaml
Type: UInt32
Parameter Sets: Content, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -IndentWithTabs
Use tabs for indentation.

```yaml
Type: Boolean
Parameter Sets: Content, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -JslintHappy
Enable jslint-happy formatting.

```yaml
Type: Boolean
Parameter Sets: Content, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -KeepArrayIndentation
Keep array indentation.

```yaml
Type: Boolean
Parameter Sets: Content, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -KeepFunctionIndentation
Keep function indentation.

```yaml
Type: Boolean
Parameter Sets: Content, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -MaxPreserveNewlines
Maximum number of consecutive newlines to preserve.

```yaml
Type: Single
Parameter Sets: Content, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -MaxStringLiteralLength
Maximum raw content length for each string literal chunk. Use 0 for the formatter default.

```yaml
Type: Int32
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
Optional path to write the formatted JavaScript.

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

### -Path
Path to a JavaScript file to format.

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

### -PreserveNewlines
Preserve existing newlines.

```yaml
Type: Boolean
Parameter Sets: Content, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -SplitLongStringLiterals
Split long quoted string literals into concatenated chunks.

```yaml
Type: SwitchParameter
Parameter Sets: Content, File
Aliases: SplitLongLine, SplitLongString
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -WrapLineLength
Line length at which formatter may wrap before the next token. Use 0 to disable token wrapping.

```yaml
Type: Int32
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
