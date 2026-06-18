---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/PSParseHTML
schema: 2.0.0
---
# ConvertFrom-HtmlSamlResponse
## SYNOPSIS
Decodes a SAMLResponse value into a safe metadata summary.

## SYNTAX
### Handoff (Default)
```powershell
ConvertFrom-HtmlSamlResponse [-Handoff] <Object> [-IncludeXml] [-IncludeSensitiveValues] [<CommonParameters>]
```

### SamlResponse
```powershell
ConvertFrom-HtmlSamlResponse [-SamlResponse] <string> [-IncludeXml] [-IncludeSensitiveValues] [<CommonParameters>]
```

## DESCRIPTION
Decodes a SAMLResponse value into a safe metadata summary.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-HtmlBrowserSsoHandoff -Session $session -IncludeSensitiveValues | ConvertFrom-HtmlSamlResponse
```


## PARAMETERS

### -Handoff
SSO handoff object returned by Get-HtmlBrowserSsoHandoff.

```yaml
Type: Object
Parameter Sets: Handoff
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: True
```

### -IncludeSensitiveValues
Reveal subject values and unredacted XML. Use only for authorized troubleshooting.

```yaml
Type: SwitchParameter
Parameter Sets: Handoff, SamlResponse
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -IncludeXml
Include decoded XML in the output. Values remain redacted unless IncludeSensitiveValues is also set.

```yaml
Type: SwitchParameter
Parameter Sets: Handoff, SamlResponse
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -SamlResponse
Raw, URL-encoded, base64-encoded, or XML SAMLResponse value.

```yaml
Type: String
Parameter Sets: SamlResponse
Aliases: Response
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByPropertyName)
Accept wildcard characters: True
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `System.String
System.Object`

## OUTPUTS

- `HtmlTinkerX.HtmlSamlResponseSummary`

## RELATED LINKS

- None
