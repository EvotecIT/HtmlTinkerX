Import-Module '.\PSParseHTML.psd1' -Force

$Path = Join-Path $PSScriptRoot '\Input\headless_table.html'
$Content = Get-Content -LiteralPath $Path -Raw

$Tables = ConvertFrom-HtmlTable -Content $Content
$Tables[0] | Format-Table -AutoSize

$Tables[1] | Format-Table -AutoSize

$Tables[2] | Format-Table -AutoSize

$Tables[3] | Format-Table -AutoSize