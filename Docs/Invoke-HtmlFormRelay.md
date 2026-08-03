---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Invoke-HtmlFormRelay
## SYNOPSIS
Follows deterministic hidden-form relay pages without launching a browser.

## SYNTAX
### Url (Default)
```powershell
Invoke-HtmlFormRelay [-Url] <uri> [-MaxRelayCount <int>] [-AllowCrossHost] [-AllowedHost <string[]>] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

### Content
```powershell
Invoke-HtmlFormRelay [-Content] <string> -BaseUrl <uri> [-MaxRelayCount <int>] [-AllowCrossHost] [-AllowedHost <string[]>] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

### Path
```powershell
Invoke-HtmlFormRelay [-Path] <string> -BaseUrl <uri> [-MaxRelayCount <int>] [-AllowCrossHost] [-AllowedHost <string[]>] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

## DESCRIPTION
Follows deterministic hidden-form relay pages without launching a browser.

## EXAMPLES

### EXAMPLE 1
```powershell
Invoke-HtmlFormRelay -Url https://example.org/login/callback -AllowCrossHost
```


## PARAMETERS

### -AllowCrossHost
Allow relay form actions to post to another host.

```yaml
Type: SwitchParameter
Parameter Sets: Url, Content, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -AllowedHost
Specific hosts allowed for cross-host relay actions.

```yaml
Type: String[]
Parameter Sets: Url, Content, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -BaseUrl
Base URL used to resolve form actions when Content or Path is used.

```yaml
Type: Uri
Parameter Sets: Content, Path
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Content
HTML content containing the first possible relay form.

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

### -MaxRelayCount
Maximum number of relay forms to submit.

```yaml
Type: Int32
Parameter Sets: Url, Content, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
Path to an HTML file containing the first possible relay form.

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
Proxy server address used when downloading or submitting relay forms.

```yaml
Type: String
Parameter Sets: Url, Content, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ProxyCredential
Credentials used for the proxy server.

```yaml
Type: PSCredential
Parameter Sets: Url, Content, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Url
URL returning the first possible relay form.

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `System.String`

## OUTPUTS

- `HtmlTinkerX.HtmlFormRelayResult`

## RELATED LINKS

- None
