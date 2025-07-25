Import-Module .\PSParseHTML.psd1 -Force

$path = Join-Path $PSScriptRoot 'Input/route_page.html'
$uri = [System.Uri]::new($path).AbsoluteUri
$session = Invoke-HTMLRendering -Url $uri -Session
$session.NetworkLogLimit = 2
Start-Sleep -Milliseconds 500
Get-HTMLNetworkLog -Session $session | Format-Table Method, Url
Close-HTMLSession -Session $session
