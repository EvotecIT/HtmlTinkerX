Import-Module .\PSParseHTML.psd1 -Force

$Path = "$PSScriptRoot\Input\Test.html"

ConvertFrom-HtmlTable -Content (Get-Content -LiteralPath $Path -Raw) -SkipFooter | Format-Table