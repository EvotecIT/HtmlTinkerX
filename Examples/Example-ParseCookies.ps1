Import-Module ..\PSParseHTML.psd1 -Force

# Parse a Netscape cookie line
$data = "example.com`tFALSE`t/`tTRUE`t1704067199`tSessionId`tabc123xyz"
$cookies = ConvertFrom-HTMLCookie -Content $data -Format Netscape

# Add cookies to a browser session
$session = Start-HtmlBrowserSession -Url 'about:blank'
Set-HtmlBrowserCookie -Session $session -Cookie $cookies
Get-HtmlBrowserCookie -Session $session
Close-HtmlBrowserSession -Session $session

# Parse a Set-Cookie header
$header = 'Set-Cookie: id=abc; Path=/; Secure'
$cookie = ConvertFrom-HTMLCookie -Content $header -Format SetCookie
$cookie
