Import-Module './PSParseHTML.psd1' -Force

$Path = Join-Path $PSScriptRoot 'Input\easy_table_with_footer.html'
$Content = Get-Content -LiteralPath $Path -Raw

$Tables = ConvertFrom-HtmlTable -Content $Content -SkipFooter
$Tables[0] | Format-Table -AutoSize
