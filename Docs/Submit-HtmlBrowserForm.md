---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Submit-HtmlBrowserForm
## SYNOPSIS
Cmdlet that submits an HTML form using Playwright or HTTP requests.

## SYNTAX
### Http (Default)
```powershell
Submit-HtmlBrowserForm [-Form] <psobject> [[-FieldValue] <hashtable>] [-Proxy <string>] [-ProxyCredential <pscredential>] [-Timeout <int>] [-MaximumResponseBytes <int>] [<CommonParameters>]
```

### Session
```powershell
Submit-HtmlBrowserForm [-Form] <psobject> [-FieldValue] <hashtable> [-Session <HtmlBrowserSession>] [-Timeout <int>] [-PassThru] [-OnFailureEvidence] [-FailureEvidenceFolder <string>] [<CommonParameters>]
```

### HttpClient
```powershell
Submit-HtmlBrowserForm [-Form] <psobject> [[-FieldValue] <hashtable>] -HttpClient <HttpClient> [-Timeout <int>] [-MaximumResponseBytes <int>] [<CommonParameters>]
```

## DESCRIPTION
Cmdlet that submits an HTML form using Playwright or HTTP requests.

## EXAMPLES

### EXAMPLE 1
```powershell
Submit-HtmlBrowserForm -Form $form -FieldValue @{ tag = @('first', 'second') }
```

Retains successful defaults and replaces all values named tag with the two supplied values.

### EXAMPLE 2
```powershell
Submit-HtmlBrowserForm -Form $form -HttpClient $client -FieldValue @{ displayName = 'Ada' }
```

Reuses the downloading client's cookies without changing or disposing that client.

## PARAMETERS

### -FailureEvidenceFolder
Root folder where failure evidence is written when OnFailureEvidence is used.

```yaml
Type: String
Parameter Sets: Session
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -FieldValue
Field overrides by name. HTTP submission retains other successful values; arrays supply repeated values.

```yaml
Type: Hashtable
Parameter Sets: Http, Session, HttpClient
Aliases: None
Possible values:

Required: False
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Form
Form object created by ConvertFrom-HtmlForm.

```yaml
Type: PSObject
Parameter Sets: Http, Session, HttpClient
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -HttpClient
Reusable HTTP client for submitting the form. The caller retains ownership, cookies, and configuration.

```yaml
Type: HttpClient
Parameter Sets: HttpClient
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaximumResponseBytes
Maximum HTTP response body bytes. Default: 16 MiB. Raise explicitly for trusted large responses.

```yaml
Type: Int32
Parameter Sets: Http, HttpClient
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -OnFailureEvidence
Export screenshots, HTML, text, Markdown, network summary, locator suggestions, and failure context if browser form submission fails.

```yaml
Type: SwitchParameter
Parameter Sets: Session
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PassThru
Return session object when using Playwright.

```yaml
Type: SwitchParameter
Parameter Sets: Session
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Proxy
Proxy server address for HTTP submission.

```yaml
Type: String
Parameter Sets: Http
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ProxyCredential
Proxy credentials for HTTP submission.

```yaml
Type: PSCredential
Parameter Sets: Http
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Session
Existing browser session for Playwright submission.

```yaml
Type: HtmlBrowserSession
Parameter Sets: Session
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Timeout
Timeout in milliseconds for browser operations or the complete HTTP submission. Zero disables this timeout; a supplied HTTP client retains its own timeout.

```yaml
Type: Int32
Parameter Sets: Http, Session, HttpClient
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

- `System.Management.Automation.PSObject`

## OUTPUTS

- `System.String`

## RELATED LINKS

- None
