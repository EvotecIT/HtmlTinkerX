Import-Module .\PSParseHTML.psd1 -Force

$path = Join-Path $PSScriptRoot 'Input/console_page.html'
$uri = [System.Uri]::new($path).AbsoluteUri
$session = Start-HtmlBrowserSession -Url $uri
Start-Sleep -Milliseconds 500
Get-HtmlBrowserConsoleLog -Session $session -Severity Error | Format-Table
Close-HtmlBrowserSession -Session $session

