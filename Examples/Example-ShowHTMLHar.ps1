$null = Import-Module ./PSParseHTML.psd1 -Force

# Example HAR file is stored inside the Input folder
$har = Join-Path $PSScriptRoot 'Input/sample.har'
$viewer = Show-HtmlBrowserHar -Path $har -OutFile (Join-Path $PSScriptRoot 'example.html')
$viewer.Log.entries | Format-Table
