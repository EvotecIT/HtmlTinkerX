---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/PSParseHTML
schema: 2.0.0
---
# ConvertFrom-HtmlJsonWebToken
## SYNOPSIS
Decodes an OAuth or OpenID Connect JSON Web Token into a safe metadata summary.

## SYNTAX
### Handoff (Default)
```powershell
ConvertFrom-HtmlJsonWebToken [-Handoff] <Object> [-FieldName <string>] [-IncludeJson] [-IncludeSensitiveValues] [<CommonParameters>]
```

### Token
```powershell
ConvertFrom-HtmlJsonWebToken [-Token] <string> [-IncludeJson] [-IncludeSensitiveValues] [<CommonParameters>]
```

## DESCRIPTION
Decodes an OAuth or OpenID Connect JSON Web Token into a safe metadata summary.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-HtmlBrowserSsoHandoff -Session $session -IncludeSensitiveValues | ConvertFrom-HtmlJsonWebToken
```


## PARAMETERS

### -FieldName
Specific handoff field name to inspect. Defaults to id_token and then access_token.

```yaml
Type: String
Parameter Sets: Handoff
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

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

### -IncludeJson
Include decoded header and payload JSON. Payload values remain redacted unless IncludeSensitiveValues is also set.

```yaml
Type: SwitchParameter
Parameter Sets: Handoff, Token
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -IncludeSensitiveValues
Reveal subject and user-identifying claim values. Use only for authorized troubleshooting.

```yaml
Type: SwitchParameter
Parameter Sets: Handoff, Token
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Token
Raw compact JSON Web Token value.

```yaml
Type: String
Parameter Sets: Token
Aliases: Jwt, JsonWebToken
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

- `HtmlTinkerX.HtmlJsonWebTokenSummary`

## RELATED LINKS

- None
