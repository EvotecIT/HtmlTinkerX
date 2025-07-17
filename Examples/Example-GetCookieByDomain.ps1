Import-Module ..\PSParseHTML.psd1 -Force

$session = Invoke-HTMLRendering -Url 'about:blank' -Session

$c1 = [PSParseHTML.HtmlCookie]::new()
$c1.Name = 'c1'
$c1.Value = 'v1'
$c1.Domain = 'example.com'
$c1.Path = '/'

$c2 = [PSParseHTML.HtmlCookie]::new()
$c2.Name = 'c2'
$c2.Value = 'v2'
$c2.Domain = 'other.com'
$c2.Path = '/'

Set-HTMLCookie -Session $session -Cookie ([PSParseHTML.HtmlCookie[]]@($c1, $c2))

# Retrieve only cookies from example.com
Get-HTMLCookie -Session $session -Domain 'example.com'

Close-HTMLSession -Session $session
