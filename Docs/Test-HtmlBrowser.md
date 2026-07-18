---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Test-HtmlBrowser
## SYNOPSIS
Tests a URL or local HTML file for network errors, console errors, and performance issues.

## SYNTAX
### Url
```powershell
Test-HtmlBrowser [-Url] <string> [-Engine <HtmlBrowserEngine>] [-Timeout <int>] [-Headless] [-Proxy <string>] [-ProxyCredential <pscredential>] [-PerformanceOnly] [-ErrorsOnly] [-CssResource <string>] [<CommonParameters>]
```

### File
```powershell
Test-HtmlBrowser [-Path] <string> [-Engine <HtmlBrowserEngine>] [-Timeout <int>] [-Headless] [-Proxy <string>] [-ProxyCredential <pscredential>] [-PerformanceOnly] [-ErrorsOnly] [-CssResource <string>] [<CommonParameters>]
```

## DESCRIPTION
Tests a URL or local HTML file for network errors, console errors, and performance issues.

## EXAMPLES

### EXAMPLE 1
```powershell
Test-HtmlBrowser -Path 'C:\Path'
```


## PARAMETERS

### -CssResource
Test for specific CSS resource.

```yaml
Type: String
Parameter Sets: Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Engine
Browser engine to use.

```yaml
Type: HtmlBrowserEngine
Parameter Sets: Url, File
Aliases: None
Possible values: Chromium, Firefox, WebKit

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -ErrorsOnly
Return only console errors.

```yaml
Type: SwitchParameter
Parameter Sets: Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Headless
Enable headless mode.

```yaml
Type: SwitchParameter
Parameter Sets: Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Path
Path to local HTML file to test.

```yaml
Type: String
Parameter Sets: File
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -PerformanceOnly
Return only performance metrics.

```yaml
Type: SwitchParameter
Parameter Sets: Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Proxy
Proxy URL to use.

```yaml
Type: String
Parameter Sets: Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -ProxyCredential
Proxy credentials.

```yaml
Type: PSCredential
Parameter Sets: Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Timeout
Timeout in milliseconds.

```yaml
Type: Int32
Parameter Sets: Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Url
URL to test.

```yaml
Type: String
Parameter Sets: Url
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

- `System.String`

## OUTPUTS

- `HtmlTinkerX.HtmlBrowserTestResult`

## RELATED LINKS

- None
