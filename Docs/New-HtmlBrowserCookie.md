---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# New-HtmlBrowserCookie
## SYNOPSIS
Cmdlet that creates a new HtmlCookie instance.

## SYNTAX
### __AllParameterSets
```powershell
New-HtmlBrowserCookie [-Name] <string> [-Value] <string> [-Domain <string>] [-Path <string>] [-Url <string>] [-Expires <Int64>] [-HttpOnly] [-Secure] [-SameSite <SameSiteAttribute>] [<CommonParameters>]
```

## DESCRIPTION
Cmdlet that creates a new HtmlCookie instance.

## EXAMPLES

### EXAMPLE 1
```powershell
New-HtmlBrowserCookie -Path 'C:\Path'
```


## PARAMETERS

### -Domain
Cookie domain.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Expires
Cookie expiration time as UNIX timestamp.

```yaml
Type: Int64
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -HttpOnly
Mark cookie as HTTP only.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Name
Cookie name.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
Cookie path.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SameSite
SameSite attribute.

```yaml
Type: SameSiteAttribute
Parameter Sets: __AllParameterSets
Aliases: None
Possible values: Strict, Lax, None

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Secure
Mark cookie as secure.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Url
Cookie URL.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Value
Cookie value.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `None`

## OUTPUTS

- `HtmlTinkerX.HtmlCookie`

## RELATED LINKS

- None
