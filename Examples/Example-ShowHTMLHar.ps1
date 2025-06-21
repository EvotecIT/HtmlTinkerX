Import-Module ./PSParseHTML.psd1 -Force

$har = Join-Path $PSScriptRoot 'example.har'
$viewer = Show-HTMLHar -Path $har -OutFile (Join-Path $PSScriptRoot 'example.html')
$viewer.Log.entries | Format-Table
