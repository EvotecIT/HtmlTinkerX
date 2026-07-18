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
Submit-HtmlBrowserForm [-Form] <psobject> [-FieldValue] <hashtable> [-Proxy <string>] [-ProxyCredential <pscredential>] [-Timeout <int>] [<CommonParameters>]
```

### Session
```powershell
Submit-HtmlBrowserForm [-Form] <psobject> [-FieldValue] <hashtable> [-Session <HtmlBrowserSession>] [-Timeout <int>] [-PassThru] [-OnFailureEvidence] [-FailureEvidenceFolder <string>] [<CommonParameters>]
```

## DESCRIPTION
Cmdlet that submits an HTML form using Playwright or HTTP requests.

## EXAMPLES

### EXAMPLE 1
```powershell
Submit-HtmlBrowserForm -FailureEvidenceFolder 'Value'
```


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
Accept wildcard characters: True
```

### -FieldValue
Hashtable of field values keyed by name.

```yaml
Type: Hashtable
Parameter Sets: Http, Session
Aliases: None
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Form
Form object created by ConvertFrom-HtmlForm.

```yaml
Type: PSObject
Parameter Sets: Http, Session
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
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
Accept wildcard characters: True
```

### -Timeout
Timeout for Playwright operations.

```yaml
Type: Int32
Parameter Sets: Http, Session
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

- `System.Management.Automation.PSObject`

## OUTPUTS

- `System.String`

## RELATED LINKS

- None
