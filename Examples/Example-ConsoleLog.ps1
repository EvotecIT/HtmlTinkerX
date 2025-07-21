Import-Module .\PSParseHTML.psd1 -Force

$path = Join-Path $PSScriptRoot 'Input/console_page.html'
$uri = [System.Uri]::new($path).AbsoluteUri
$session = Invoke-HTMLRendering -Url $uri -Session
Start-Sleep -Milliseconds 500
Get-HTMLConsoleLog -Session $session -Severity Error | Format-Table
Close-HTMLSession -Session $session


