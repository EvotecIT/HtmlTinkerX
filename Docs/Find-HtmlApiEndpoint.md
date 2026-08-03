---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Find-HtmlApiEndpoint
## SYNOPSIS
Finds and classifies API, JavaScript, and form endpoints from a page workbench or HTML input.

## SYNTAX
### Workbench (Default)
```powershell
Find-HtmlApiEndpoint [-Workbench] <HtmlPageWorkbenchResult> [-ExcludeForms] [-ExcludeScriptEndpoints] [<CommonParameters>]
```

### Content
```powershell
Find-HtmlApiEndpoint [-Content] <string> [-BaseUrl <uri>] [-IncludeLinkedScripts] [-IncludeExternalLinkedScripts] [-ExcludeForms] [-ExcludeScriptEndpoints] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

### Url
```powershell
Find-HtmlApiEndpoint [-Url] <uri> [-IncludeLinkedScripts] [-IncludeExternalLinkedScripts] [-ExcludeForms] [-ExcludeScriptEndpoints] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

### Path
```powershell
Find-HtmlApiEndpoint [-Path] <string> [-BaseUrl <uri>] [-IncludeLinkedScripts] [-IncludeExternalLinkedScripts] [-ExcludeForms] [-ExcludeScriptEndpoints] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

## DESCRIPTION
Finds and classifies API, JavaScript, and form endpoints from a page workbench or HTML input.

## EXAMPLES

### EXAMPLE 1
```powershell
Invoke-HtmlPageWorkbench -Url https://example.com | Find-HtmlApiEndpoint
```


## PARAMETERS

### -BaseUrl
Base URL used to resolve relative endpoints when Content or Path is used.

```yaml
Type: Uri
Parameter Sets: Content, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
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
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -ExcludeForms
Omits form actions from the endpoint inventory.

```yaml
Type: SwitchParameter
Parameter Sets: Workbench, Content, Url, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ExcludeScriptEndpoints
Omits JavaScript-discovered endpoints from the endpoint inventory.

```yaml
Type: SwitchParameter
Parameter Sets: Workbench, Content, Url, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeExternalLinkedScripts
Allows linked-script endpoint inspection to download cross-origin scripts.

```yaml
Type: SwitchParameter
Parameter Sets: Content, Url, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeLinkedScripts
Downloads same-origin linked JavaScript files and inspects them for endpoints.

```yaml
Type: SwitchParameter
Parameter Sets: Content, Url, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
Path to a local HTML file to inspect.

```yaml
Type: String
Parameter Sets: Path
Aliases: File
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Proxy
Proxy server address used when downloading by URL or inspecting linked scripts.

```yaml
Type: String
Parameter Sets: Content, Url, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ProxyCredential
Credentials used with the proxy server.

```yaml
Type: PSCredential
Parameter Sets: Content, Url, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Url
URL of the page to download and inspect.

```yaml
Type: Uri
Parameter Sets: Url
Aliases: Uri
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Workbench
Page workbench result to inspect.

```yaml
Type: HtmlPageWorkbenchResult
Parameter Sets: Workbench
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `HtmlTinkerX.HtmlPageWorkbenchResult`
- `System.String`

## OUTPUTS

- `HtmlTinkerX.HtmlApiEndpointRecord`

## RELATED LINKS

- None
