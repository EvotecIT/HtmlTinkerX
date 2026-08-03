---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# ConvertFrom-HtmlForm
## SYNOPSIS
Extracts HTML form information into PowerShell objects.

## SYNTAX
### Content (Default)
```powershell
ConvertFrom-HtmlForm -Content <string> [-IncludeMetadata] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

### Url
```powershell
ConvertFrom-HtmlForm -Url <uri> [-IncludeMetadata] [-Proxy <string>] [-ProxyCredential <pscredential>] [<CommonParameters>]
```

## DESCRIPTION
Extracts HTML form information into PowerShell objects.

## EXAMPLES

### EXAMPLE 1
```powershell
ConvertFrom-HtmlForm -Url https://example.com
```


## PARAMETERS

### -Content
HTML content containing forms.

```yaml
Type: String
Parameter Sets: Content
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue, ByPropertyName)
Accept wildcard characters: False
```

### -IncludeMetadata
Include additional metadata like form index and CSS classes.

```yaml
Type: SwitchParameter
Parameter Sets: Content, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Proxy
Proxy server address for downloading when using Url.

```yaml
Type: String
Parameter Sets: Content, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ProxyCredential
Credentials for the proxy server.

```yaml
Type: PSCredential
Parameter Sets: Content, Url
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Url
URL of a page with forms.

```yaml
Type: Uri
Parameter Sets: Url
Aliases: Uri
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `System.String`

## OUTPUTS

- `System.Management.Automation.PSObject`

## RELATED LINKS

- None
