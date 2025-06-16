Import-Module .\PSParseHTML.psd1 -Force

# Read HTML from example file
$Path = Join-Path $PSScriptRoot 'Input/sample_form.html'
$Content = Get-Content -LiteralPath $Path -Raw

# Extract forms
$Forms = ConvertFrom-HtmlForm -Content $Content
$Forms | Format-Table -AutoSize

