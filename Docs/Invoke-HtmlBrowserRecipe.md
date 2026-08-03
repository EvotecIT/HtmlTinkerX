---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Invoke-HtmlBrowserRecipe
## SYNOPSIS
Executes a replayable browser automation recipe.

## SYNTAX
### Recipe (Default)
```powershell
Invoke-HtmlBrowserRecipe [-Recipe] <HtmlBrowserRecipe> [-Session <HtmlBrowserSession>] [-ProfilePath <string>] [-UserDataDirectory <string>] [-StatePath <string>] [-Browser <HtmlBrowserEngine>] [-Scenario <HtmlBrowserScenario>] [-BrowserChannel <string>] [-BrowserExecutablePath <string>] [-BrowserArgument <string[]>] [-ChromiumSandbox] [-Clean] [-Visible] [-SlowMo <int>] [-NavigationTimeout <int>] [-Credential <pscredential>] [-LoginUrl <string>] [-UsernameSelector <string>] [-PasswordSelector <string>] [-SubmitSelector <string>] [-ManualLogin] [-LoginSuccessSelector <string>] [-LoginTimeout <int>] [-PreventSsoAutoSubmit] [-Username <string>] [-Password <string>] [-Proxy <string>] [-ProxyCredential <pscredential>] [-UserAgent <string>] [-Locale <string>] [-ViewportWidth <Int32>] [-ViewportHeight <Int32>] [-ScreenWidth <Int32>] [-ScreenHeight <Int32>] [-DeviceScaleFactor <Double>] [-Mobile] [-Touch] [-GeoLatitude <Double>] [-GeoLongitude <Double>] [-Timezone <string>] [-Permission <string[]>] [-InitScript <string[]>] [-InitScriptPath <string[]>] [-BlockResourceType <HtmlNetworkResourceType[]>] [-BlockResourcePattern <string[]>] [-Variable <IDictionary>] [-VariablePath <string>] [-SkipPreflight] [-StrictPreflight] [-OnFailureEvidence] [-FailureEvidenceFolder <string>] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

### Path
```powershell
Invoke-HtmlBrowserRecipe [-Path] <string> [-Session <HtmlBrowserSession>] [-ProfilePath <string>] [-UserDataDirectory <string>] [-StatePath <string>] [-Browser <HtmlBrowserEngine>] [-Scenario <HtmlBrowserScenario>] [-BrowserChannel <string>] [-BrowserExecutablePath <string>] [-BrowserArgument <string[]>] [-ChromiumSandbox] [-Clean] [-Visible] [-SlowMo <int>] [-NavigationTimeout <int>] [-Credential <pscredential>] [-LoginUrl <string>] [-UsernameSelector <string>] [-PasswordSelector <string>] [-SubmitSelector <string>] [-ManualLogin] [-LoginSuccessSelector <string>] [-LoginTimeout <int>] [-PreventSsoAutoSubmit] [-Username <string>] [-Password <string>] [-Proxy <string>] [-ProxyCredential <pscredential>] [-UserAgent <string>] [-Locale <string>] [-ViewportWidth <Int32>] [-ViewportHeight <Int32>] [-ScreenWidth <Int32>] [-ScreenHeight <Int32>] [-DeviceScaleFactor <Double>] [-Mobile] [-Touch] [-GeoLatitude <Double>] [-GeoLongitude <Double>] [-Timezone <string>] [-Permission <string[]>] [-InitScript <string[]>] [-InitScriptPath <string[]>] [-BlockResourceType <HtmlNetworkResourceType[]>] [-BlockResourcePattern <string[]>] [-Variable <IDictionary>] [-VariablePath <string>] [-SkipPreflight] [-StrictPreflight] [-OnFailureEvidence] [-FailureEvidenceFolder <string>] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

## DESCRIPTION
Executes a replayable browser automation recipe.

## EXAMPLES

### EXAMPLE 1
```powershell
Invoke-HtmlBrowserRecipe -Path .\browser.recipe.json
```


### EXAMPLE 2
```powershell
$recipe | Invoke-HtmlBrowserRecipe -Session $session
```


### EXAMPLE 3
```powershell
Invoke-HtmlBrowserRecipe -Path .\browser.recipe.json -VariablePath .\browser.recipe.variables.json
```


### EXAMPLE 4
```powershell
Invoke-HtmlBrowserRecipe -Path .\browser.recipe.json -SkipPreflight
```


### EXAMPLE 5
```powershell
Invoke-HtmlBrowserRecipe -Path .\browser.recipe.json -StrictPreflight
```


## PARAMETERS

### -BlockResourcePattern
Playwright URL glob patterns to abort before navigation, such as **/analytics/**.

```yaml
Type: String[]
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -BlockResourceType
Browser resource types to abort before navigation, such as Image, Media, Font, or Stylesheet.

```yaml
Type: HtmlNetworkResourceType[]
Parameter Sets: Recipe, Path
Aliases: None
Possible values: Document, Stylesheet, Image, Media, Font, Script, TextTrack, XHR, Fetch, EventSource, WebSocket, Manifest, Other

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Browser
Browser engine to use when the recipe creates a session.

```yaml
Type: HtmlBrowserEngine
Parameter Sets: Recipe, Path
Aliases: None
Possible values: Chromium, Firefox, WebKit

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -BrowserArgument
Additional browser command-line arguments.

```yaml
Type: String[]
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -BrowserChannel
Browser distribution channel, such as chrome, msedge, chromium, chrome-beta, or msedge-dev.

```yaml
Type: String
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -BrowserExecutablePath
Path to a browser executable.

```yaml
Type: String
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -CancellationToken
Token used to cancel the operation.

```yaml
Type: CancellationToken
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ChromiumSandbox
Enable Chromium sandboxing when supported.

```yaml
Type: SwitchParameter
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Clean
Force browser runtime reinstall before launch.

```yaml
Type: SwitchParameter
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Credential
Credentials used when accessing authenticated pages.

```yaml
Type: PSCredential
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DeviceScaleFactor
Scaling factor for high DPI devices.

```yaml
Type: Double
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -FailureEvidenceFolder
Root folder where recipe failure evidence is written when OnFailureEvidence is used.

```yaml
Type: String
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -GeoLatitude
Latitude used for geolocation.

```yaml
Type: Double
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -GeoLongitude
Longitude used for geolocation.

```yaml
Type: Double
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -InitScript
JavaScript snippets evaluated before page scripts run.

```yaml
Type: String[]
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -InitScriptPath
JavaScript files evaluated before page scripts run.

```yaml
Type: String[]
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Locale
Locale used by the browser context, such as en-US or pl-PL.

```yaml
Type: String
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LoginSuccessSelector
CSS selector that indicates manual login completed successfully.

```yaml
Type: String
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LoginTimeout
Timeout in milliseconds used when waiting for LoginSuccessSelector.

```yaml
Type: Int32
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LoginUrl
Login page URL used for form-based authentication before navigating to the recipe StartUrl.

```yaml
Type: String
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ManualLogin
Open a visible browser for manual MFA/SSO login before replaying recipe steps.

```yaml
Type: SwitchParameter
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Mobile
Expose mobile browser behavior where supported.

```yaml
Type: SwitchParameter
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -NavigationTimeout
Navigation timeout in milliseconds used when the recipe creates a session.

```yaml
Type: Int32
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -OnFailureEvidence
Export screenshots, HTML, text, Markdown, network summary, locator suggestions, and failure context when a recipe step fails.

```yaml
Type: SwitchParameter
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Password
Password for pages secured with basic authentication.

```yaml
Type: String
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PasswordSelector
CSS selector for the password field used with LoginUrl.

```yaml
Type: String
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
Recipe JSON path.

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

### -Permission
Browser permissions granted to pages in the context.

```yaml
Type: String[]
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PreventSsoAutoSubmit
Prevent recognized SSO handoff forms from auto-submitting so hidden assertion fields can be inspected.

```yaml
Type: SwitchParameter
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ProfilePath
Optional browser profile JSON file used as launch defaults when the recipe creates a session.

```yaml
Type: String
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Proxy
Proxy server address used when launching the browser.

```yaml
Type: String
Parameter Sets: Recipe, Path
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
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Recipe
Recipe object to execute.

```yaml
Type: HtmlBrowserRecipe
Parameter Sets: Recipe
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Scenario
Intent-focused browser automation defaults to apply before explicit parameter values.

```yaml
Type: HtmlBrowserScenario
Parameter Sets: Recipe, Path
Aliases: None
Possible values: Custom, AuditProof, MailboxProof, LoginProtected, SinglePageApp, LowBandwidth, NetworkCapture, DownloadEvidence

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ScreenHeight
Screen height in pixels.

```yaml
Type: Int32
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ScreenWidth
Screen width in pixels.

```yaml
Type: Int32
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Session
Existing browser session. When omitted, the recipe must provide StartUrl.

```yaml
Type: HtmlBrowserSession
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SkipPreflight
Skip default preflight validation before launching or using a browser session.

```yaml
Type: SwitchParameter
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SlowMo
Delay Playwright actions by the specified milliseconds.

```yaml
Type: Int32
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -StatePath
Playwright storage-state JSON file for cookies and local storage.

```yaml
Type: String
Parameter Sets: Recipe, Path
Aliases: StorageStatePath
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -StrictPreflight
Treat preflight warnings as blocking issues before launching or using a browser session.

```yaml
Type: SwitchParameter
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SubmitSelector
CSS selector for the submit button used with LoginUrl.

```yaml
Type: String
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Timezone
Timezone identifier used by the browser JavaScript runtime.

```yaml
Type: String
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Touch
Expose touch input where supported.

```yaml
Type: SwitchParameter
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -UserAgent
User agent string used by the browser context.

```yaml
Type: String
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -UserDataDirectory
Persistent browser user-data directory for cookies, storage, cache, and permissions.

```yaml
Type: String
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Username
Username for pages secured with basic authentication.

```yaml
Type: String
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -UsernameSelector
CSS selector for the username field used with LoginUrl.

```yaml
Type: String
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Variable
Runtime variables used to replace redacted or parameterized recipe step values.

```yaml
Type: IDictionary
Parameter Sets: Recipe, Path
Aliases: RecipeVariable
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -VariablePath
JSON file containing runtime variables. Placeholder values such as <secret> are ignored so templates cannot replay as literal secrets.

```yaml
Type: String
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ViewportHeight
Viewport height in pixels.

```yaml
Type: Int32
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ViewportWidth
Viewport width in pixels.

```yaml
Type: Int32
Parameter Sets: Recipe, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Visible
Show the browser instead of running headless.

```yaml
Type: SwitchParameter
Parameter Sets: Recipe, Path
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

- `HtmlTinkerX.HtmlBrowserRecipe`

## OUTPUTS

- `HtmlTinkerX.HtmlBrowserRecipeRunResult`

## RELATED LINKS

- None
