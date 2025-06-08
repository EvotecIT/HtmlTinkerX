Import-Module .\PSParseHTML.psd1 -Force

$HTML = Get-RenderedHtml -Url "https://www.evotec.xyz"
$HTML

$HTML = Get-RenderedHtml -Url "https://www.evotec.xyz" -OutFile "$PSScriptRoot\Output\evotec.html"
$HTML