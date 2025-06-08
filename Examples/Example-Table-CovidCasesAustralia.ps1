Import-Module .\PSParseHTML.psd1 -Force

$HTML = Invoke-HTMLRendering -Url 'https://infogram.com/daily-summary-of-covid-19-in-australia-1hzj4on55vpp2pw'

$Tables1 | Format-List

$Tables1 = ConvertFrom-HtmlTable -Content $HTML -IncludeMetadata
$Tables1[0].Data | Format-Table -AutoSize

$Tables1[1].Data | Format-Table