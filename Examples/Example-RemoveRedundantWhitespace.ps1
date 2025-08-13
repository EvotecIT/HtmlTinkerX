Import-Module .\PSParseHTML.psd1 -Force

$Html = '<div>   Hello  </div>  <span>  World</span>'
$Normalized = [HtmlTinkerX.HtmlUtilities]::RemoveRedundantWhitespace($Html)
$Normalized
