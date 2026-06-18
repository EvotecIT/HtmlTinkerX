Import-Module ./PSParseHTML.psd1 -Force

$har = Show-HtmlBrowserHar -Path (Join-Path $PSScriptRoot 'Input/sample.har')
Export-HtmlBrowserHar -Har $har -OutFile (Join-Path $PSScriptRoot 'copy.har')
