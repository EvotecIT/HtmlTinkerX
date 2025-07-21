Import-Module ./PSParseHTML.psd1 -Force

$markup = '<div itemscope itemtype="https://schema.org/Person"><span itemprop="foo">bar</span></div>'
$mismatches = Test-HtmlMicrodata -Content $markup
$mismatches | Format-Table
