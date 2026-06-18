---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/PSParseHTML
schema: 2.0.0
---
# Select-HtmlJavaScriptConfig
## SYNOPSIS
Selects JavaScript application configuration objects and framework state from inline HTML scripts.

## SYNTAX
### Node (Default)
```powershell
Select-HtmlJavaScriptConfig [-HtmlNode] <Object> [-Name <string[]>] [-Contains] [-StartsWith] [-PropertyPath <string[]>] [-NoAppState] [-Tolerant] [<CommonParameters>]
```

### Content
```powershell
Select-HtmlJavaScriptConfig -Content <string> [-Name <string[]>] [-Contains] [-StartsWith] [-PropertyPath <string[]>] [-NoAppState] [-Tolerant] [<CommonParameters>]
```

### File
```powershell
Select-HtmlJavaScriptConfig -Path <string> [-Name <string[]>] [-Contains] [-StartsWith] [-PropertyPath <string[]>] [-NoAppState] [-Tolerant] [<CommonParameters>]
```

### Url
```powershell
Select-HtmlJavaScriptConfig -Url <uri> [-Name <string[]>] [-Contains] [-StartsWith] [-PropertyPath <string[]>] [-NoAppState] [-Tolerant] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

## DESCRIPTION
Selects JavaScript application configuration objects and framework state from inline HTML scripts.

## EXAMPLES

### EXAMPLE 1
```powershell
Select-HtmlJavaScriptConfig -Content $html
```


### EXAMPLE 2
```powershell
Select-HtmlJavaScriptConfig -Content $html -Name window.__CONFIG__ -PropertyPath api.baseUrl
```


### EXAMPLE 3
```powershell
Select-HtmlNode -Content $html -XPath '//body' | Select-HtmlJavaScriptConfig -Name settings -Contains
```


## PARAMETERS

### -Contains
Matches variable names or full assignment paths that contain the provided Name values.

```yaml
Type: SwitchParameter
Parameter Sets: Node, Content, File, Url
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
Position: named
Default value: None
Accept pipeline input: True (ByValue, ByPropertyName)
Accept wildcard characters: True
```

### -HtmlNode
HtmlAgilityPack node or document to inspect.

```yaml
Type: Object
Parameter Sets: Node
Aliases: Node, InputObject
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: True
```

### -Name
Variable names or assignment paths to return. When omitted, common config and state names are searched.

```yaml
Type: String[]
Parameter Sets: Node, Content, File, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -NoAppState
Skips known framework app-state payloads and returns only JavaScript variable matches.

```yaml
Type: SwitchParameter
Parameter Sets: Node, Content, File, Url
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
Parameter Sets: Node, Content, File, Url
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
Parameter Sets: Node, Content, File, Url
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
Parameter Sets: Node, Content, File, Url
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

- `System.String
System.Object`

## OUTPUTS

- `HtmlTinkerX.HtmlJavaScriptConfigItem`

## RELATED LINKS

- None
