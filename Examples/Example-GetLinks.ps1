Import-Module .\PSParseHTML.psd1 -Force

$URL = "https://developer.mozilla.org/en-US/docs/Web/HTML/Element/em"
$html = (Invoke-WebRequest -Uri $UrL).Content

$Classes = ConvertFrom-HTMLAttributes -Content $HTML -Class 'sidebar' -ReturnObject
$Classes | Format-Table

$Objects = ConvertFrom-HTMLTag -Content $Classes[0].InnerHtml -Tag 'a' -ReturnObject
$Objects.href

$URL = "https://developer.mozilla.org/en-US/docs/Web/HTML/Element/em"
$html = (Invoke-WebRequest -Uri $UrL).Content
$Objects = ConvertFrom-HTMLTag -Content $html -Tag 'option' -ReturnObject
$Objects