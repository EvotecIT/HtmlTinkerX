---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/PSParseHTML
schema: 2.0.0
---
# ConvertFrom-HtmlCookie
## SYNOPSIS
Converts various cookie representations into HtmlCookie objects.

## SYNTAX
### Content (Default)
```powershell
ConvertFrom-HtmlCookie -Content <string> [-Format <HtmlCookieFormat>] [<CommonParameters>]
```

### File
```powershell
ConvertFrom-HtmlCookie -Path <string> [-Format <HtmlCookieFormat>] [<CommonParameters>]
```

## DESCRIPTION
Converts various cookie representations into HtmlCookie objects.

## EXAMPLES

### EXAMPLE 1
```powershell
ConvertFrom-HtmlCookie -Content "Set-Cookie: id=42; Path=/" -Format SetCookie
```


## PARAMETERS

### -Content
Cookie data to parse.

```yaml
Type: String
Parameter Sets: Content
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue, ByPropertyName)
Accept wildcard characters: True
```

### -Format
Input format.

```yaml
Type: HtmlCookieFormat
Parameter Sets: Content, File
Aliases: None
Possible values: Netscape, SetCookie, OrgJson, CookieStore, Puppeteer

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Path
Path to a cookie file.

```yaml
Type: String
Parameter Sets: File
Aliases: File
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `System.String`

## OUTPUTS

- `HtmlTinkerX.HtmlCookie`

## RELATED LINKS

- None
