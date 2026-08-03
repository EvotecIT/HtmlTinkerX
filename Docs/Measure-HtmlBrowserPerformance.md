---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Measure-HtmlBrowserPerformance
## SYNOPSIS
Measures page load performance for a URL or local HTML file.

## SYNTAX
### Url (Default)
```powershell
Measure-HtmlBrowserPerformance [-Url] <string> [-Engine <HtmlBrowserEngine>] [-Headless] [-Timeout <int>] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

### File
```powershell
Measure-HtmlBrowserPerformance [-Path] <string> [-Engine <HtmlBrowserEngine>] [-Headless] [-Timeout <int>] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

## DESCRIPTION
Measures page load performance for a URL or local HTML file.

## EXAMPLES

### EXAMPLE 1
```powershell
Measure-HtmlBrowserPerformance -Path 'C:\Path'
```


## PARAMETERS

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
Accept wildcard characters: False
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
Accept wildcard characters: False
```

### -Path
Path to local HTML file.

```yaml
Type: String
Parameter Sets: File
Aliases: File
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Proxy
Proxy URL.

```yaml
Type: String
Parameter Sets: Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
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
Accept wildcard characters: False
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
Accept wildcard characters: False
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
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `System.String`

## OUTPUTS

- `HtmlTinkerX.HtmlPerformanceMetrics`

## RELATED LINKS

- None
