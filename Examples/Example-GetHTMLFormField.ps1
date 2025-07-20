Import-Module .\PSParseHTML.psd1 -Force

# Load HTML from example file
$Path = Join-Path $PSScriptRoot 'Input/sample_form.html'
$Content = Get-Content -LiteralPath $Path -Raw

# Retrieve form fields
$Fields = Get-HTMLFormField -Content $Content
# Each field now exposes a strongly-typed 'Type' property
$Fields | Format-Table -AutoSize
