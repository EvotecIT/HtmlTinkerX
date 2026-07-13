---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Optimize-Email
## SYNOPSIS
Cmdlet that inlines CSS for email bodies using PreMailer.Net.

## SYNTAX
### Body (Default)
```powershell
Optimize-Email -Body <string> [-BaseUri <uri>] [-RemoveStyleElements] [-IgnoreElements <string>] [-Css <string>] [-CssFilePath <string>] [-StripIdAndClassAttributes] [-RemoveComments] [-PreserveMediaQueries] [-UseEmailFormatter] [-DownloadRemoteCss] [-HttpClient <HttpClient>] [-AddAnalyticsTags] [-AnalyticsSource <string>] [-AnalyticsMedium <string>] [-AnalyticsCampaign <string>] [-AnalyticsContent <string>] [-AnalyticsDomain <string>] [<CommonParameters>]
```

### File
```powershell
Optimize-Email -Path <string> [-BaseUri <uri>] [-RemoveStyleElements] [-IgnoreElements <string>] [-Css <string>] [-CssFilePath <string>] [-StripIdAndClassAttributes] [-RemoveComments] [-PreserveMediaQueries] [-UseEmailFormatter] [-DownloadRemoteCss] [-HttpClient <HttpClient>] [-AddAnalyticsTags] [-AnalyticsSource <string>] [-AnalyticsMedium <string>] [-AnalyticsCampaign <string>] [-AnalyticsContent <string>] [-AnalyticsDomain <string>] [<CommonParameters>]
```

## DESCRIPTION
Cmdlet that inlines CSS for email bodies using PreMailer.Net.

## EXAMPLES

### EXAMPLE 1
```powershell
Optimize-Email -Body $html -RemoveComments
```


## PARAMETERS

### -AddAnalyticsTags
Add Google Analytics tags.

```yaml
Type: SwitchParameter
Parameter Sets: Body, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -AnalyticsCampaign
Value for utm_campaign.

```yaml
Type: String
Parameter Sets: Body, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -AnalyticsContent
Value for utm_content.

```yaml
Type: String
Parameter Sets: Body, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -AnalyticsDomain
Analytics domain.

```yaml
Type: String
Parameter Sets: Body, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -AnalyticsMedium
Value for utm_medium.

```yaml
Type: String
Parameter Sets: Body, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -AnalyticsSource
Value for utm_source.

```yaml
Type: String
Parameter Sets: Body, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -BaseUri
Base URI for resolving relative URLs.

```yaml
Type: Uri
Parameter Sets: Body, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Body
HTML content to process.

```yaml
Type: String
Parameter Sets: Body
Aliases: Content
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: True
```

### -Css
Additional CSS content to inline.

```yaml
Type: String
Parameter Sets: Body, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -CssFilePath
Path to a CSS file to include.

```yaml
Type: String
Parameter Sets: Body, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -DownloadRemoteCss
Download CSS from <link> elements.

```yaml
Type: SwitchParameter
Parameter Sets: Body, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -HttpClient
HTTP client used to download linked stylesheets. The caller retains ownership of the client.

```yaml
Type: HttpClient
Parameter Sets: Body, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -IgnoreElements
CSS selector for elements to ignore.

```yaml
Type: String
Parameter Sets: Body, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -Path
Path to a HTML file to process.

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

### -PreserveMediaQueries
Preserve media queries from style nodes.

```yaml
Type: SwitchParameter
Parameter Sets: Body, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -RemoveComments
Remove comments from HTML and CSS.

```yaml
Type: SwitchParameter
Parameter Sets: Body, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -RemoveStyleElements
Remove <style> elements after inlining.

```yaml
Type: SwitchParameter
Parameter Sets: Body, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -StripIdAndClassAttributes
Strip id and class attributes from output.

```yaml
Type: SwitchParameter
Parameter Sets: Body, File
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: True
```

### -UseEmailFormatter
Use the email formatter when generating HTML.

```yaml
Type: SwitchParameter
Parameter Sets: Body, File
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

- `System.String`

## OUTPUTS

- `System.String`

## RELATED LINKS

- None
