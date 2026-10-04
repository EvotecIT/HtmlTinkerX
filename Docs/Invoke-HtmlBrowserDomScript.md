---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Invoke-HtmlBrowserDomScript
## SYNOPSIS
Cmdlet that executes JavaScript against HTML using AngleSharp.Js.

## SYNTAX
### Content (Default)
```powershell
Invoke-HtmlBrowserDomScript -Content <string> -Script <string> [-Timeout <int>] [-MaximumStatements <int>] [-MaximumMemoryBytes <long>] [-MaximumHtmlCharacters <int>] [-MaximumScriptCharacters <int>] [-SkipPageScripts] [<CommonParameters>]
```

### Path
```powershell
Invoke-HtmlBrowserDomScript -Path <string> -Script <string> [-Timeout <int>] [-MaximumStatements <int>] [-MaximumMemoryBytes <long>] [-MaximumHtmlCharacters <int>] [-MaximumScriptCharacters <int>] [-SkipPageScripts] [<CommonParameters>]
```

## DESCRIPTION
Cmdlet that executes JavaScript against HTML using AngleSharp.Js.

## EXAMPLES

### EXAMPLE 1
```powershell
Invoke-HtmlBrowserDomScript -Content 'Value' -Script 'Value'
```


### EXAMPLE 2
```powershell
Invoke-HtmlBrowserDomScript -Path 'C:\Path' -Script 'Value'
```


## PARAMETERS

### -Content
HTML content to process.

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

### -MaximumHtmlCharacters
Maximum decoded HTML input length, including file input. Defaults to 16777216 characters.

```yaml
Type: Int32
Parameter Sets: Content, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaximumMemoryBytes
Maximum managed bytes allocated by each script execution. This does not bound the DOM or process memory.

```yaml
Type: Int64
Parameter Sets: Content, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaximumScriptCharacters
Maximum requested script length. Defaults to 1048576 characters.

```yaml
Type: Int32
Parameter Sets: Content, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaximumStatements
Maximum statements per script execution. Defaults to 1000000.

```yaml
Type: Int32
Parameter Sets: Content, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
Path to a HTML file.

```yaml
Type: String
Parameter Sets: Path
Aliases: File
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Script
JavaScript code to run.

```yaml
Type: String
Parameter Sets: Content, Path
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SkipPageScripts
Skip scripts and event handlers in the HTML and evaluate only the requested script.

```yaml
Type: SwitchParameter
Parameter Sets: Content, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Timeout
Whole DOM execution timeout in milliseconds, including inline page scripts. Defaults to 10000.

```yaml
Type: Int32
Parameter Sets: Content, Path
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

- `System.String`

## OUTPUTS

- `System.Object`

## RELATED LINKS

- None
