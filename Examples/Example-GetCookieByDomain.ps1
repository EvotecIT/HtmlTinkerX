Import-Module ..\PSParseHTML.psd1 -Force

$session = Invoke-HTMLRendering -Url 'about:blank' -Session

$c1 = New-HTMLCookie -Name 'c1' -Value 'v1' -Domain 'example.com' -Path '/'

$c2 = New-HTMLCookie -Name 'c2' -Value 'v2' -Domain 'other.com' -Path '/'

Set-HTMLCookie -Session $session -Cookie ([HtmlTinkerX.HtmlCookie[]]@($c1, $c2))

# Retrieve only cookies from example.com
Get-HTMLCookie -Session $session -Domain 'example.com'

Close-HTMLSession -Session $session
