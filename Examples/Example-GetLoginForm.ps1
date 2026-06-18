Import-Module .\PSParseHTML.psd1 -Force

# Detect login form selectors from a webpage
$form = Get-HtmlBrowserLoginForm -Url 'https://example.com/login'
$form | Format-List

# Using an existing session
$session = Start-HtmlBrowserSession -Url 'https://example.com/login'
Get-HtmlBrowserLoginForm -Session $session | Format-List
Close-HtmlBrowserSession -Session $session | Out-Null
