Import-Module .\PSParseHTML.psd1 -Force

$path = Join-Path $PSScriptRoot 'Input/sample_form.html'
Get-HTMLLoginForm -Path $path
