Import-Module .\PSParseHTML.psd1 -Force

$HTML = Invoke-HtmlRendering -Url 'https://www.goal.com/en-us/premier-league/table/2kwbbcootiqqgmrzs6o5inle5'

$Test = ConvertFrom-HtmlTable -Content $HTML -Engine AgilityPack
$Test | Format-Table -AutoSize *

$Test = ConvertFrom-HtmlTable -Content $HTML -Engine AngleSharp
$Test | Format-Table -AutoSize *