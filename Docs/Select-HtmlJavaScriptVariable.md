---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Select-HtmlJavaScriptVariable
## SYNOPSIS
Selects JavaScript declarations and assignments from inline JavaScript script tags in HTML.

## SYNTAX
### Content (Default)
```powershell
Select-HtmlJavaScriptVariable [-Content] <string> [-Name <string[]>] [-Contains] [-StartsWith] [-DeclarationOnly] [-PropertyPath <string[]>] [-Tolerant] [<CommonParameters>]
```

### File
```powershell
Select-HtmlJavaScriptVariable -Path <string> [-Name <string[]>] [-Contains] [-StartsWith] [-DeclarationOnly] [-PropertyPath <string[]>] [-Tolerant] [<CommonParameters>]
```

### Url
```powershell
Select-HtmlJavaScriptVariable -Url <uri> [-Name <string[]>] [-Contains] [-StartsWith] [-DeclarationOnly] [-PropertyPath <string[]>] [-Tolerant] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

## DESCRIPTION
Selects JavaScript declarations and assignments from inline JavaScript script tags in HTML.

## EXAMPLES

### EXAMPLE 1
```powershell
$html = @'

window.$Config = {
    auth: {
        sCtx: "expected-context"
    }
};

'@

Select-HtmlJavaScriptVariable -Content $html -Name '$Config' -PropertyPath auth.sCtx
```


### EXAMPLE 2
```powershell
$html = @'

import value from "./settings.js";
window.$Config = { sCtx: "from-module" };

window.$Config = { sCtx: "from-script" };

'@

Select-HtmlJavaScriptVariable -Content $html -Name '$Config' -PropertyPath sCtx |
    Select-Object -First 1
```


### EXAMPLE 3
```powershell
$html = @'
{"name":"schema"}
$Config = { sCtx: "first" };
$Config = { sCtx: "second" };

'@

Select-HtmlJavaScriptVariable -Content $html -Name '$Config' -PropertyPath sCtx
```


### EXAMPLE 4
```powershell
$html = @'

$Config = { sCtx: "first" };

$Config = { sCtx: "second" };

'@

$values = Select-HtmlJavaScriptVariable -Content $html -Name '$Config' -PropertyPath sCtx
$values | Select-Object -First 1
$values | Select-Object -Last 1
```


## PARAMETERS

### -Contains
Matches variable names or full assignment paths that contain the provided Name values.

```yaml
Type: SwitchParameter
Parameter Sets: Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Content
HTML content to inspect.

```yaml
Type: String
Parameter Sets: Content
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue, ByPropertyName)
Accept wildcard characters: True
```

### -DeclarationOnly
Returns only variable declarations and skips loose assignment expressions.

```yaml
Type: SwitchParameter
Parameter Sets: Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Name
Variable or assignment target names to return.

```yaml
Type: String[]
Parameter Sets: Content, File, Url
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

### -PropertyPath
Returns a value from a dotted property path inside the matched JavaScript object or array literal.

```yaml
Type: String[]
Parameter Sets: Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Proxy
Proxy server address used when downloading by URL.

```yaml
Type: String
Parameter Sets: Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -ProxyCredential
Credentials used with the proxy server.

```yaml
Type: PSCredential
Parameter Sets: Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -StartsWith
Matches variable names or full assignment paths that start with the provided Name values.

```yaml
Type: SwitchParameter
Parameter Sets: Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Tolerant
Enables Acornima tolerant parsing for inline script content.

```yaml
Type: SwitchParameter
Parameter Sets: Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Url
URL of an HTML page to download and inspect.

```yaml
Type: Uri
Parameter Sets: Url
Aliases: Uri
Possible values:

Required: True
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

- `System.Management.Automation.PSObject`

## RELATED LINKS

- None
