---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Start-HtmlBrowserVideoCapture
## SYNOPSIS
Cmdlet that starts recording a browser session to a WebM file.

## SYNTAX
### Session (Default)
```powershell
Start-HtmlBrowserVideoCapture [[-Session] <HtmlBrowserSession>] -OutFile <string> [-Visible] [-SlowMo <int>] [-Width <int>] [-Height <int>] [-UserAgent <string>] [-ViewportWidth <Int32>] [-ViewportHeight <Int32>] [-DeviceScaleFactor <Double>] [-GeoLatitude <Double>] [-GeoLongitude <Double>] [-Timezone <string>] [-NoDefault] [<CommonParameters>]
```

### Url
```powershell
Start-HtmlBrowserVideoCapture [-Url] <string> -OutFile <string> [-Credential <pscredential>] [-Username <string>] [-Password <string>] [-LoginUrl <string>] [-UsernameSelector <string>] [-PasswordSelector <string>] [-SubmitSelector <string>] [-Browser <HtmlBrowserEngine>] [-Clean] [-Visible] [-SlowMo <int>] [-Width <int>] [-Height <int>] [-UserAgent <string>] [-ViewportWidth <Int32>] [-ViewportHeight <Int32>] [-DeviceScaleFactor <Double>] [-GeoLatitude <Double>] [-GeoLongitude <Double>] [-Timezone <string>] [-NoDefault] [<CommonParameters>]
```

### File
```powershell
Start-HtmlBrowserVideoCapture [-Path] <string> -OutFile <string> [-Credential <pscredential>] [-Username <string>] [-Password <string>] [-LoginUrl <string>] [-UsernameSelector <string>] [-PasswordSelector <string>] [-SubmitSelector <string>] [-Browser <HtmlBrowserEngine>] [-Clean] [-Visible] [-SlowMo <int>] [-Width <int>] [-Height <int>] [-UserAgent <string>] [-ViewportWidth <Int32>] [-ViewportHeight <Int32>] [-DeviceScaleFactor <Double>] [-GeoLatitude <Double>] [-GeoLongitude <Double>] [-Timezone <string>] [-NoDefault] [<CommonParameters>]
```

## DESCRIPTION
Cmdlet that starts recording a browser session to a WebM file.

## EXAMPLES

### EXAMPLE 1
```powershell
Start-HtmlBrowserVideoCapture -OutFile 'Value'
```


## PARAMETERS

### -Browser
Engine to use when creating a new session.

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

### -Clean
Remove previous session data.

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

### -Credential
Credentials used for login.

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

### -DeviceScaleFactor
Device scale factor for emulation.

```yaml
Type: Double
Parameter Sets: Session, Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -GeoLatitude
Latitude of the emulated geolocation.

```yaml
Type: Double
Parameter Sets: Session, Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -GeoLongitude
Longitude of the emulated geolocation.

```yaml
Type: Double
Parameter Sets: Session, Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Height
Browser window height.

```yaml
Type: Int32
Parameter Sets: Session, Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LoginUrl
Login page URL.

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

### -NoDefault
Do not store the created session in PSParseHTML_DefaultSession.

```yaml
Type: SwitchParameter
Parameter Sets: Session, Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -OutFile
Path where the WebM file will be stored.

```yaml
Type: String
Parameter Sets: Session, Url, File
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Password
Password for basic authentication.

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

### -PasswordSelector
CSS selector for the password field.

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

### -Path
Path to an HTML file to open.

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

### -Session
Existing browser session to record.

```yaml
Type: HtmlBrowserSession
Parameter Sets: Session
Aliases: None
Possible values:

Required: False
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -SlowMo
Delay between Playwright actions in milliseconds.

```yaml
Type: Int32
Parameter Sets: Session, Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SubmitSelector
CSS selector for the submit button.

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

### -Timezone
Timezone identifier.

```yaml
Type: String
Parameter Sets: Session, Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Url
URL of the page to record.

```yaml
Type: String
Parameter Sets: Url
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -UserAgent
Custom User-Agent header.

```yaml
Type: String
Parameter Sets: Session, Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Username
Username for basic authentication.

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

### -UsernameSelector
CSS selector for the username field.

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

### -ViewportHeight
Viewport height override.

```yaml
Type: Int32
Parameter Sets: Session, Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ViewportWidth
Viewport width override.

```yaml
Type: Int32
Parameter Sets: Session, Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Visible
Show browser window instead of running headless.

```yaml
Type: SwitchParameter
Parameter Sets: Session, Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Width
Browser window width.

```yaml
Type: Int32
Parameter Sets: Session, Url, File
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
