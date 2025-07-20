Import-Module .\PSParseHTML.psd1 -Force

$Path = "$PSScriptRoot\Input\open_graph.html"
$Content = Get-Content -LiteralPath $Path -Raw
$OpenGraph = ConvertFrom-HtmlOpenGraph -Content $Content
$OpenGraph

# Access the raw representation
$graph = [HtmlTinkerX.HtmlParser]::ParseOpenGraph($Content)
$graph.Properties | Format-Table Name, Values
