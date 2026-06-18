---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/PSParseHTML
schema: 2.0.0
---
# ConvertFrom-HtmlList
## SYNOPSIS
Converts HTML lists into PowerShell objects by default.

## SYNTAX
### Content (Default)
```powershell
ConvertFrom-HtmlList -Content <string> [-Engine <HtmlParserEngine>] [-IncludeMetadata] [-AsString] [-TagPlaceholder <string>] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

### Url
```powershell
ConvertFrom-HtmlList -Url <uri> [-Engine <HtmlParserEngine>] [-IncludeMetadata] [-AsString] [-TagPlaceholder <string>] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

## DESCRIPTION
Converts HTML lists into PowerShell objects by default.

## EXAMPLES

### EXAMPLE 1
```powershell
ConvertFrom-HtmlList -Content $html
```


## PARAMETERS

### -AsString
Return list items as strings.

```yaml
Type: SwitchParameter
Parameter Sets: Content, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Content
HTML content containing lists.

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

### -Engine
Selects parsing engine.

```yaml
Type: HtmlParserEngine
Parameter Sets: Content, Url
Aliases: None
Possible values: AngleSharp, AgilityPack

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -IncludeMetadata
Include list metadata information.

```yaml
Type: SwitchParameter
Parameter Sets: Content, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Proxy
Proxy server address used when Url is specified.
Include protocol and port if required.

```yaml
Type: String
Parameter Sets: Content, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -ProxyCredential
Credentials used for authenticating with the specified Proxy server.

```yaml
Type: PSCredential
Parameter Sets: Content, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -TagPlaceholder
Placeholder inserted between text segments when joining item text.

```yaml
Type: String
Parameter Sets: Content, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Url
URL of a page with lists.

```yaml
Type: Uri
Parameter Sets: Url
Aliases: Uri
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

- `System.Management.Automation.PSObject[]`

## RELATED LINKS

- None
