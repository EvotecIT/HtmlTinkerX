Import-Module ..\PSParseHTML.psd1 -Force

$session = Start-HtmlBrowserSession -Url 'about:blank'

$c1 = New-HtmlBrowserCookie -Name 'c1' -Value 'v1' -Domain 'example.com' -Path '/'

$c2 = New-HtmlBrowserCookie -Name 'c2' -Value 'v2' -Domain 'other.com' -Path '/'

Set-HtmlBrowserCookie -Session $session -Cookie ([HtmlTinkerX.HtmlCookie[]]@($c1, $c2))

# Retrieve only cookies from example.com
Get-HtmlBrowserCookie -Session $session -Domain 'example.com'

Close-HtmlBrowserSession -Session $session
