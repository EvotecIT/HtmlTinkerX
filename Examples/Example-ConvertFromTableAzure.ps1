Import-Module '.\PSParseHTML.psd1' -Force

$AzureStatus = ConvertFrom-HtmlTable -Url "https://status.azure.com/en-us/status" -IncludeMetadata -AllProperties -SkipFooter
$AzureStatus[1].Data | Format-Table -AutoSize

$AzureStatus1 = ConvertFrom-HtmlTable -Url "https://status.azure.com/en-us/status" -AllProperties -SkipFooter
$AzureStatus1[1] | Format-Table -AutoSize