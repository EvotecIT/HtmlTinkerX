Import-Module ..\PSParseHTML.psd1 -Force

$state = Join-Path $PSScriptRoot 'state.json'
$session = Open-HTMLSession -Url 'about:blank'
Export-BrowserState -Session $session -Path $state
Close-HTMLSession -Session $session

$session = Import-BrowserState -Path $state -Url 'about:blank'
Get-HTMLCookie -Session $session
Close-HTMLSession -Session $session
