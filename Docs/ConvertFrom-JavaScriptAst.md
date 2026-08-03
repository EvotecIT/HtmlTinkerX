---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# ConvertFrom-JavaScriptAst
## SYNOPSIS
Parses JavaScript into an Acornima abstract syntax tree.

## SYNTAX
### Content (Default)
```powershell
ConvertFrom-JavaScriptAst -Content <string> [-Module] [-Tolerant] [-PreserveParens] [-AllowHashBang] [<CommonParameters>]
```

### File
```powershell
ConvertFrom-JavaScriptAst -Path <string> [-Module] [-Tolerant] [-PreserveParens] [-AllowHashBang] [<CommonParameters>]
```

## DESCRIPTION
Parses JavaScript into an Acornima abstract syntax tree.

## EXAMPLES

### EXAMPLE 1
```powershell
ConvertFrom-JavaScriptAst -Content 'const value = 42;'
```


## PARAMETERS

### -AllowHashBang
Allows hashbang comments at the start of scripts.

```yaml
Type: SwitchParameter
Parameter Sets: Content, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Content
JavaScript content to parse.

```yaml
Type: String
Parameter Sets: Content
Aliases: FileContent
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue, ByPropertyName)
Accept wildcard characters: False
```

### -Module
Parses the input as an ECMAScript module.

```yaml
Type: SwitchParameter
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
Path to a JavaScript file to parse.

```yaml
Type: String
Parameter Sets: File
Aliases: File
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PreserveParens
Preserves parenthesized expression nodes.

```yaml
Type: SwitchParameter
Parameter Sets: Content, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Tolerant
Enables Acornima tolerant parsing.

```yaml
Type: SwitchParameter
Parameter Sets: Content, File
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

- `System.String`

## OUTPUTS

- `Acornima.Ast.Node`

## RELATED LINKS

- None
