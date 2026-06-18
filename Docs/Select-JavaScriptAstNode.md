---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/PSParseHTML
schema: 2.0.0
---
# Select-JavaScriptAstNode
## SYNOPSIS
Returns descendant nodes from an Acornima JavaScript AST or JavaScript source.

## SYNTAX
### Source (Default)
```powershell
Select-JavaScriptAstNode [-Source] <string> [-Type <string[]>] [-IncludeRoot] [-FilterScript <scriptblock>] [-Module] [-Tolerant] [-PreserveParens] [<CommonParameters>]
```

### Ast
```powershell
Select-JavaScriptAstNode [-Ast] <Node> [-Type <string[]>] [-IncludeRoot] [-FilterScript <scriptblock>] [<CommonParameters>]
```

## DESCRIPTION
Returns descendant nodes from an Acornima JavaScript AST or JavaScript source.

## EXAMPLES

### EXAMPLE 1
```powershell
ConvertFrom-JavaScriptAst -Content 'const value = 42;' | Select-JavaScriptAstNode -Type VariableDeclaration
```


### EXAMPLE 2
```powershell
$script = @'
const settings = {
    apiKey: "abc",
    enabled: true
};
'@

Select-JavaScriptAstNode -Source $script -Type Script,ObjectExpression -IncludeRoot
```


### EXAMPLE 3
```powershell
$module = @'
import value from "./settings.js";
export const settings = {
    enabled: true
};
'@

Select-JavaScriptAstNode -Source $module -Module -Type ImportDeclaration,ExportNamedDeclaration
```


## PARAMETERS

### -Ast
Acornima AST node to inspect.

```yaml
Type: Node
Parameter Sets: Ast
Aliases: InputObject, Node
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: True
```

### -FilterScript
Optional PowerShell predicate used to filter each matched AST node. The node is passed as the first scriptblock argument.

```yaml
Type: ScriptBlock
Parameter Sets: Source, Ast
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -IncludeRoot
Includes the root node in the traversal output.

```yaml
Type: SwitchParameter
Parameter Sets: Source, Ast
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Module
Parses source input as an ECMAScript module.

```yaml
Type: SwitchParameter
Parameter Sets: Source
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -PreserveParens
Preserves parenthesized expression nodes for source input.

```yaml
Type: SwitchParameter
Parameter Sets: Source
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Source
JavaScript content to parse and inspect.

```yaml
Type: String
Parameter Sets: Source
Aliases: Content
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: True
```

### -Tolerant
Enables Acornima tolerant parsing for source input.

```yaml
Type: SwitchParameter
Parameter Sets: Source
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Type
Node type names to return, such as VariableDeclaration, ObjectExpression, or ClassBody.

```yaml
Type: String[]
Parameter Sets: Source, Ast
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

- `System.String
Acornima.Ast.Node`

## OUTPUTS

- `Acornima.Ast.Node`

## RELATED LINKS

- None
