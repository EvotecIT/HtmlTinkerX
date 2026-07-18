---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Find-HtmlDataSource
## SYNOPSIS
Discovers static, app-state, and endpoint data sources that can be extracted without starting a browser.

## SYNTAX
### Workbench (Default)
```powershell
Find-HtmlDataSource [-Workbench] <HtmlPageWorkbenchResult> [-DirectOnly] [-MaxSources <int>] [<CommonParameters>]
```

### Content
```powershell
Find-HtmlDataSource [-Content] <string> [-BaseUrl <uri>] [-IncludeLinkedScripts] [-IncludeExternalLinkedScripts] [-DirectOnly] [-MaxSources <int>] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

### Url
```powershell
Find-HtmlDataSource [-Url] <uri> [-IncludeLinkedScripts] [-IncludeExternalLinkedScripts] [-DirectOnly] [-MaxSources <int>] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

### Path
```powershell
Find-HtmlDataSource [-Path] <string> [-BaseUrl <uri>] [-IncludeLinkedScripts] [-IncludeExternalLinkedScripts] [-DirectOnly] [-MaxSources <int>] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

## DESCRIPTION
Discovers static, app-state, and endpoint data sources that can be extracted without starting a browser.

## EXAMPLES

### EXAMPLE 1
```powershell
Find-HtmlDataSource -Url https://example.org/products -IncludeLinkedScripts
```


### EXAMPLE 2
```powershell
Invoke-HtmlPageWorkbench -Content $html -BaseUrl https://example.org | Find-HtmlDataSource -DirectOnly
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
Accept pipeline input: True (ByValue)
Accept wildcard characters: True
```

### -DirectOnly
Returns only sources that HtmlTinkerX can extract directly.

```yaml
Type: SwitchParameter
Parameter Sets: Workbench, Content, Url, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -IncludeExternalLinkedScripts
Allows linked-script inspection to download cross-origin scripts.

```yaml
Type: SwitchParameter
Parameter Sets: Content, Url, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
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
Accept wildcard characters: True
```

### -MaxSources
Maximum number of sources to return.

```yaml
Type: Int32
Parameter Sets: Workbench, Content, Url, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `HtmlTinkerX.HtmlPageWorkbenchResult
System.String`

## OUTPUTS

- `HtmlTinkerX.HtmlBrowserlessDataSource`

## RELATED LINKS

- None
