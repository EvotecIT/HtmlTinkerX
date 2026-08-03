---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Start-HtmlBrowserRecipeRecording
## SYNOPSIS
Starts recording successful HtmlTinkerX browser actions into a replayable browser recipe.

## SYNTAX
### __AllParameterSets
```powershell
Start-HtmlBrowserRecipeRecording [[-Session] <HtmlBrowserSession>] [[-Name] <string>] [-StartUrl <string>] [-IncludeCurrentUrl] [-Force] [-Timeout <int>] [-NoSelectorAlternates] [-SelectorAlternateLimit <int>] [-CancellationToken <CancellationToken>] [<CommonParameters>]
```

## DESCRIPTION
Starts recording successful HtmlTinkerX browser actions into a replayable browser recipe.

## EXAMPLES

### EXAMPLE 1
```powershell
Start-HtmlBrowserRecipeRecording -Name MailboxProof -IncludeCurrentUrl
```


## PARAMETERS

### -CancellationToken
Token used to cancel the operation.

```yaml
Type: CancellationToken
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Force
Replace an active recording on the same session.

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

### -IncludeCurrentUrl
Use the current page URL as the recipe StartUrl.

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
Friendly recipe name.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -NoSelectorAlternates
Do not automatically capture stable selector alternates for recorded selector-based steps.

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

### -SelectorAlternateLimit
Maximum selector alternates captured for each recorded selector-based step.

```yaml
Type: Int32
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Session
Browser session to record. When omitted, the default PSParseHTML session is used.

```yaml
Type: HtmlBrowserSession
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -StartUrl
Explicit recipe start URL. When omitted with IncludeCurrentUrl, the current page URL is used.

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

### -Timeout
Default timeout in milliseconds stored on the recipe.

```yaml
Type: Int32
Parameter Sets: __AllParameterSets
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

- `HtmlTinkerX.HtmlBrowserSession`

## OUTPUTS

- `HtmlTinkerX.HtmlBrowserRecipe`

## RELATED LINKS

- None
