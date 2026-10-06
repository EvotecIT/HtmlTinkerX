---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Export-HtmlExtractionRecipe
## SYNOPSIS
Saves a browserless extraction recipe from a discovered data source or DOM field rules.

## SYNTAX
### Source (Default)
```powershell
Export-HtmlExtractionRecipe [-DataSource] <HtmlBrowserlessDataSource> [-Path] <string> [-IncludeRawContent] [-AcceptedResult <HtmlBrowserlessExtractionResult>] [-PassThru] [<CommonParameters>]
```

### Dom
```powershell
Export-HtmlExtractionRecipe [-Path] <string> -ItemSelector <string> -Property <IDictionary> [-BaseUrl <uri>] [-MinimumItemCount <Int32>] [-MaximumItemCount <Int32>] [-BaselineContent <string>] [-PassThru] [<CommonParameters>]
```

## DESCRIPTION
Saves a browserless extraction recipe from a discovered data source or DOM field rules.

## EXAMPLES

### EXAMPLE 1
```powershell
Find-HtmlDataSource -Content $html -DirectOnly | Select-Object -First 1 | Export-HtmlExtractionRecipe -Path .\recipe.json
```


### EXAMPLE 2
```powershell
Export-HtmlExtractionRecipe -ItemSelector '.product-card' -Property @{
    Name = @{ Selector = '.product-title'; Required = $true }
    Price = @{ Selector = '.product-price'; DataType = [decimal]; MaximumValueCount = 1 }
} -MinimumItemCount 1 -Path .\products.json
```


### EXAMPLE 3
```powershell
Export-HtmlExtractionRecipe -ItemSelector '.product' -Property @{
    Name = @{ Selector = 'h2'; Required = $true }
    Note = '.note'
} -BaselineContent $acceptedHtml -Path .\products.json
$result = Invoke-HtmlExtractionRecipe -Path .\products.json -Content $currentHtml
$result.DriftReport
```


## PARAMETERS

### -AcceptedResult
Successful structured-data extraction whose inspected output structure is accepted as a baseline.

```yaml
Type: HtmlBrowserlessExtractionResult
Parameter Sets: Source
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -BaselineContent
Accepted HTML used to capture DOM item structure and collection confidence in the saved recipe.

```yaml
Type: String
Parameter Sets: Dom
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -BaseUrl
Page URL used to resolve relative field URLs when the recipe is evaluated.

```yaml
Type: Uri
Parameter Sets: Dom
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DataSource
Browserless data source to save as a recipe.

```yaml
Type: HtmlBrowserlessDataSource
Parameter Sets: Source
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -IncludeRawContent
Includes raw static payloads in the recipe. Review recipe files before sharing them.

```yaml
Type: SwitchParameter
Parameter Sets: Source
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ItemSelector
CSS selector matching repeated DOM items.

```yaml
Type: String
Parameter Sets: Dom
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaximumItemCount
Maximum acceptable number of items when a DOM recipe is evaluated.

```yaml
Type: Int32
Parameter Sets: Dom
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MinimumItemCount
Minimum acceptable number of items when a DOM recipe is evaluated.

```yaml
Type: Int32
Parameter Sets: Dom
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PassThru
Writes the recipe path to the pipeline.

```yaml
Type: SwitchParameter
Parameter Sets: Source, Dom
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
Destination JSON recipe path.

```yaml
Type: String
Parameter Sets: Source, Dom
Aliases: OutFile
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Property
DOM property-to-selector map using the same field rules as Select-HtmlData.

```yaml
Type: IDictionary
Parameter Sets: Dom
Aliases: None
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

- `HtmlTinkerX.HtmlBrowserlessDataSource`

## OUTPUTS

- `System.String`

## RELATED LINKS

- None
