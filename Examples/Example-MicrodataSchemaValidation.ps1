# Demonstrates validating microdata items with schema definitions
$markup = '<div itemscope itemtype="https://schema.org/Person"><span itemprop="foo">bar</span></div>'
$items = ConvertFrom-HtmlMicrodata -Content $markup
$mismatches = [HtmlTinkerX.HtmlParser]::ValidateMicrodataItems($items)
$mismatches | Format-Table
