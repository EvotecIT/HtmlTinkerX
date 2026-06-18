---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/PSParseHTML
schema: 2.0.0
---
# ConvertTo-HtmlWebRequestSession
## SYNOPSIS
Converts browser session cookies or HtmlCookie objects into a PowerShell WebRequestSession.

## SYNTAX
### Session (Default)
```powershell
ConvertTo-HtmlWebRequestSession [[-Session] <HtmlBrowserSession>] [-Domain <string[]>] [-UserAgent <string>] [-Header <hashtable>] [-IncludeExpired] [-Quiet] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

### Cookie
```powershell
ConvertTo-HtmlWebRequestSession [-Cookie] <HtmlCookie[]> [-UserAgent <string>] [-Header <hashtable>] [-IncludeExpired] [-Quiet] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

## DESCRIPTION
Converts browser session cookies or HtmlCookie objects into a PowerShell WebRequestSession.

## EXAMPLES

### EXAMPLE 1
```powershell
$session = Start-HtmlBrowserSession -Url https://portal.contoso.example -Visible -ManualLogin
$webSession = ConvertTo-HtmlWebRequestSession -Session $session
Invoke-WebRequest -Uri https://portal.contoso.example/report -WebSession $webSession
```


## PARAMETERS

### -CancellationToken
Token used to cancel the operation.

```yaml
Type: CancellationToken
Parameter Sets: Session, Cookie
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Cookie
Cookies to copy into a PowerShell WebRequestSession.

```yaml
Type: HtmlCookie[]
Parameter Sets: Cookie
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: True
```

### -Domain
Cookie domain filter used when reading cookies from a browser session.

```yaml
Type: String[]
Parameter Sets: Session
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Header
Optional headers to add to the WebRequestSession.

```yaml
Type: Hashtable
Parameter Sets: Session, Cookie
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -IncludeExpired
Include expired cookies instead of skipping them.

```yaml
Type: SwitchParameter
Parameter Sets: Session, Cookie
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Quiet
Suppress warnings about browser cookies that cannot be represented by System.Net.Cookie.

```yaml
Type: SwitchParameter
Parameter Sets: Session, Cookie
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Session
Browser session whose cookies should be copied. When omitted, the default PSParseHTML session is used.

```yaml
Type: HtmlBrowserSession
Parameter Sets: Session
Aliases: None
Possible values:

Required: False
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: True
```

### -UserAgent
Optional User-Agent to set on the WebRequestSession.

```yaml
Type: String
Parameter Sets: Session, Cookie
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

- `HtmlTinkerX.HtmlBrowserSession
HtmlTinkerX.HtmlCookie[]`

## OUTPUTS

- `System.Management.Automation.PSObject`

## RELATED LINKS

- None
