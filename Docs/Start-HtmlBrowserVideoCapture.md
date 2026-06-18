---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/PSParseHTML
schema: 2.0.0
---
# Start-HtmlBrowserVideoCapture
## SYNOPSIS
Cmdlet that starts recording a browser session to a WebM file.

## SYNTAX
### Session (Default)
```powershell
Start-HtmlBrowserVideoCapture [[-Session] <HtmlBrowserSession>] -OutFile <string> [-Visible] [-SlowMo <int>] [-Width <int>] [-Height <int>] [-UserAgent <string>] [-ViewportWidth <int>] [-ViewportHeight <int>] [-DeviceScaleFactor <double>] [-GeoLatitude <double>] [-GeoLongitude <double>] [-Timezone <string>] [-NoDefault] [<CommonParameters>]
```

### Url
```powershell
Start-HtmlBrowserVideoCapture [-Url] <string> -OutFile <string> [-Credential <pscredential>] [-Username <string>] [-Password <string>] [-LoginUrl <string>] [-UsernameSelector <string>] [-PasswordSelector <string>] [-SubmitSelector <string>] [-Browser <HtmlBrowserEngine>] [-Clean] [-Visible] [-SlowMo <int>] [-Width <int>] [-Height <int>] [-UserAgent <string>] [-ViewportWidth <int>] [-ViewportHeight <int>] [-DeviceScaleFactor <double>] [-GeoLatitude <double>] [-GeoLongitude <double>] [-Timezone <string>] [-NoDefault] [<CommonParameters>]
```

### File
```powershell
Start-HtmlBrowserVideoCapture [-Path] <string> -OutFile <string> [-Credential <pscredential>] [-Username <string>] [-Password <string>] [-LoginUrl <string>] [-UsernameSelector <string>] [-PasswordSelector <string>] [-SubmitSelector <string>] [-Browser <HtmlBrowserEngine>] [-Clean] [-Visible] [-SlowMo <int>] [-Width <int>] [-Height <int>] [-UserAgent <string>] [-ViewportWidth <int>] [-ViewportHeight <int>] [-DeviceScaleFactor <double>] [-GeoLatitude <double>] [-GeoLongitude <double>] [-Timezone <string>] [-NoDefault] [<CommonParameters>]
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
```

### -DeviceScaleFactor
Device scale factor for emulation.

```yaml
Type: Nullable`1
Parameter Sets: Session, Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -GeoLatitude
Latitude of the emulated geolocation.

```yaml
Type: Nullable`1
Parameter Sets: Session, Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -GeoLongitude
Longitude of the emulated geolocation.

```yaml
Type: Nullable`1
Parameter Sets: Session, Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
```

### -ViewportHeight
Viewport height override.

```yaml
Type: Nullable`1
Parameter Sets: Session, Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -ViewportWidth
Viewport width override.

```yaml
Type: Nullable`1
Parameter Sets: Session, Url, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `HtmlTinkerX.HtmlBrowserSession`

## OUTPUTS

- `HtmlTinkerX.HtmlBrowserSession`

## RELATED LINKS

- None
