---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Invoke-HtmlBrowserNavigation
## SYNOPSIS
Cmdlet that navigates an existing browser session to a new URL.

## SYNTAX
### ByUrl (Default)
```powershell
Invoke-HtmlBrowserNavigation [[-Session] <HtmlBrowserSession>] [-Url] <string> [-LoadState <HtmlBrowserLoadState>] [-PassThru] [-OnFailureEvidence] [-FailureEvidenceFolder <string>] [-Timeout <int>] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

### ByText
```powershell
Invoke-HtmlBrowserNavigation [[-Session] <HtmlBrowserSession>] [-Text] <string> [-Exact] [-Regex <string>] [-WaitForNavigation] [-LoadState <HtmlBrowserLoadState>] [-NavigationUrl <string>] [-PassThru] [-OnFailureEvidence] [-FailureEvidenceFolder <string>] [-Timeout <int>] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

### BySelector
```powershell
Invoke-HtmlBrowserNavigation [[-Session] <HtmlBrowserSession>] [-Selector] <string> [-WaitForNavigation] [-LoadState <HtmlBrowserLoadState>] [-NavigationUrl <string>] [-PassThru] [-OnFailureEvidence] [-FailureEvidenceFolder <string>] [-Timeout <int>] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

## DESCRIPTION
Cmdlet that navigates an existing browser session to a new URL.

## EXAMPLES

### EXAMPLE 1
```powershell
Invoke-HtmlBrowserNavigation -CancellationToken 'Value'
```


## PARAMETERS

### -CancellationToken
Token used to cancel the operation.

```yaml
Type: CancellationToken
Parameter Sets: ByUrl, ByText, BySelector
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Exact
Use exact text match.

```yaml
Type: SwitchParameter
Parameter Sets: ByText
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -FailureEvidenceFolder
Root folder where failure evidence is written when OnFailureEvidence is used.

```yaml
Type: String
Parameter Sets: ByUrl, ByText, BySelector
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LoadState
Browser readiness state used for direct URL navigation and click-triggered navigation waits.

```yaml
Type: HtmlBrowserLoadState
Parameter Sets: ByUrl, ByText, BySelector
Aliases: WaitUntil
Possible values: Commit, DomContentLoaded, Load, NetworkIdle

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -NavigationUrl
Expected post-click navigation URL glob used with WaitForNavigation.

```yaml
Type: String
Parameter Sets: ByText, BySelector
Aliases: WaitForUrl, UrlPattern
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -OnFailureEvidence
Export screenshots, HTML, text, Markdown, network summary, and failure context if navigation or click fails.

```yaml
Type: SwitchParameter
Parameter Sets: ByUrl, ByText, BySelector
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PassThru
Return the session object.

```yaml
Type: SwitchParameter
Parameter Sets: ByUrl, ByText, BySelector
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Regex
Regular expression for text match.

```yaml
Type: String
Parameter Sets: ByText
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Selector
CSS selector of the element to click.

```yaml
Type: String
Parameter Sets: BySelector
Aliases: None
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Session
Existing browser session.

```yaml
Type: HtmlBrowserSession
Parameter Sets: ByUrl, ByText, BySelector
Aliases: None
Possible values:

Required: False
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Text
Text of the element to click.

```yaml
Type: String
Parameter Sets: ByText
Aliases: None
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Timeout
Timeout in milliseconds for navigation and clicks.

```yaml
Type: Int32
Parameter Sets: ByUrl, ByText, BySelector
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Url
Destination URL.

```yaml
Type: String
Parameter Sets: ByUrl
Aliases: None
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -WaitForNavigation
Wait for navigation event after clicking.

```yaml
Type: SwitchParameter
Parameter Sets: ByText, BySelector
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

- `HtmlTinkerX.HtmlBrowserSession`

## OUTPUTS

- `HtmlTinkerX.HtmlBrowserSession`

## RELATED LINKS

- None
