Import-Module .\PSParseHTML.psd1 -Force

$path = Join-Path $PSScriptRoot 'Input/route_page.html'
$uri = [System.Uri]::new($path).AbsoluteUri
$session = Start-HtmlBrowserSession -Url $uri
$session.NetworkLogLimit = 2
Start-Sleep -Milliseconds 500
Get-HtmlBrowserNetworkLog -Session $session | Format-Table Method, Url
Close-HtmlBrowserSession -Session $session
