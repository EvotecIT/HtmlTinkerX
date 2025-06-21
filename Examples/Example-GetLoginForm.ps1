Import-Module .\PSParseHTML.psd1 -Force

# Detect login form selectors from a webpage
$form = Get-HTMLLoginForm -Url 'https://example.com/login'
$form | Format-List

# Using an existing session
$session = Open-HTMLSession -Url 'https://example.com/login' -Session
Get-HTMLLoginForm -Session $session | Format-List
Close-HTMLSession -Session $session | Out-Null
