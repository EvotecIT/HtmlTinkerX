Import-Module ./PSParseHTML.psd1 -Force

$har = Show-HTMLHar -Path (Join-Path $PSScriptRoot 'Input/sample.har')
Save-HTMLHar -Har $har -OutFile (Join-Path $PSScriptRoot 'copy.har')
