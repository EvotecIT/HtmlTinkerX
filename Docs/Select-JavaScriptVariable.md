---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/PSParseHTML
schema: 2.0.0
---
# Select-JavaScriptVariable
## SYNOPSIS
Finds variable declarations and assignment expressions in an Acornima JavaScript AST or JavaScript source.

## SYNTAX
### Source (Default)
```powershell
Select-JavaScriptVariable [-Source] <string> [-Name <string[]>] [-Contains] [-StartsWith] [-DeclarationOnly] [-PropertyPath <string[]>] [-Tolerant] [<CommonParameters>]
```

### Ast
```powershell
Select-JavaScriptVariable [-Ast] <Node> [-Name <string[]>] [-Contains] [-StartsWith] [-DeclarationOnly] [-PropertyPath <string[]>] [<CommonParameters>]
```

## DESCRIPTION
Finds variable declarations and assignment expressions in an Acornima JavaScript AST or JavaScript source.

## EXAMPLES

### EXAMPLE 1
```powershell
ConvertFrom-JavaScriptAst -Content 'const token = "abc";' | Select-JavaScriptVariable -Name token
```


### EXAMPLE 2
```powershell
$script = @'
$Config = {
    "fShowPersistentCookiesWarning": false,
    "urlMsaLogout": "https://example.com/logout",
    "sCtx": "expected-context"
}
'@

Select-JavaScriptVariable -Source $script -Name '$Config' -PropertyPath sCtx
```


### EXAMPLE 3
```powershell
$script = @'
$Config = { sCtx: "first" };
$Config = { sCtx: "second" };
'@

Select-JavaScriptVariable -Source $script -Name '$Config' -PropertyPath sCtx |
    Select-Object -Last 1
```


### EXAMPLE 4
```powershell
$script = @'
window.$Config = {
    auth: {
        urls: {
            logout: "https://example.com/logout"
        }
    }
}
'@

Select-JavaScriptVariable -Source $script -Name '$Config' -PropertyPath auth.urls.logout
```


### EXAMPLE 5
```powershell
$script = @'
$Config += suffix;
$Config = { sCtx: "final" };
'@

Select-JavaScriptVariable -Source $script -Name '$Config' -PropertyPath sCtx
```


### EXAMPLE 6
```powershell
$script = @'
const cfg = {
    [key]: "dynamic",
    token: "old",
    ...override,
    items: ["first", ...extra, "last"],
    safe: "after"
};

const enabled = !window.disabled;
'@

Select-JavaScriptVariable -Source $script -Name cfg -PropertyPath key,token,items.0,items.2,safe
Select-JavaScriptVariable -Source $script -Name enabled
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

### -Contains
Matches variable names that contain the provided Name values.

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

### -DeclarationOnly
Returns only variable declarations and skips loose assignment expressions.

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

### -Name
Variable names to return.

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

### -PropertyPath
Returns a value from a dotted property path inside the matched JavaScript object or array literal.

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

### -StartsWith
Matches variable names that start with the provided Name values.

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

### -Tolerant
Enables Acornima tolerant parsing for JavaScript source input.

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `System.String
Acornima.Ast.Node`

## OUTPUTS

- `System.Management.Automation.PSObject`

## RELATED LINKS

- None
