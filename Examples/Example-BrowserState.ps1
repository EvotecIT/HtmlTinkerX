Import-Module ..\PSParseHTML.psd1 -Force

$state = Join-Path $PSScriptRoot 'state.json'
$session = Start-HtmlBrowserSession -Url 'about:blank'
Export-HtmlBrowserState -Session $session -Path $state
Close-HtmlBrowserSession -Session $session

$session = Import-HtmlBrowserState -Path $state -Url 'about:blank'
Get-HtmlBrowserCookie -Session $session
Close-HtmlBrowserSession -Session $session
