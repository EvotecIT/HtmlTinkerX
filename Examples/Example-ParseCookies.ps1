Import-Module ..\PSParseHTML.psd1 -Force

# Parse a Netscape cookie line
$data = "example.com`tFALSE`t/`tTRUE`t1704067199`tSessionId`tabc123xyz"
$cookies = ConvertFrom-HTMLCookie -Content $data -Format Netscape

# Add cookies to a browser session
$session = Invoke-HTMLRendering -Url 'about:blank' -Session
Set-HTMLCookie -Session $session -Cookie $cookies
Get-HTMLCookie -Session $session
Close-HTMLSession -Session $session

# Parse a Set-Cookie header
$header = 'Set-Cookie: id=abc; Path=/; Secure'
$cookie = ConvertFrom-HTMLCookie -Content $header -Format SetCookie
$cookie
