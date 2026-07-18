---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# ConvertFrom-HtmlSsoHandoff
## SYNOPSIS
Analyzes an SSO handoff form and safely decodes known protocol artifacts.

## SYNTAX
### __AllParameterSets
```powershell
ConvertFrom-HtmlSsoHandoff [-Handoff] <Object> [-IncludeXml] [-IncludeJson] [-IncludeSensitiveValues] [<CommonParameters>]
```

## DESCRIPTION
Analyzes an SSO handoff form and safely decodes known protocol artifacts.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-HtmlBrowserSsoHandoff -Session $session -IncludeSensitiveValues | ConvertFrom-HtmlSsoHandoff
```


## PARAMETERS

### -Handoff
SSO handoff object returned by Get-HtmlBrowserSsoHandoff.

```yaml
Type: Object
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: True
```

### -IncludeJson
Include decoded JWT header and payload JSON when id_token or access_token fields are present. Values remain redacted unless IncludeSensitiveValues is also set.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -IncludeSensitiveValues
Reveal subject, user-identifying, and assertion values in nested summaries. Use only for authorized troubleshooting.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -IncludeXml
Include decoded SAML XML when a SAMLResponse is present. Values remain redacted unless IncludeSensitiveValues is also set.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
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

- `System.Object`

## OUTPUTS

- `HtmlTinkerX.HtmlSsoHandoffAnalysis`

## RELATED LINKS

- None
