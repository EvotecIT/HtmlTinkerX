Import-Module ./PSParseHTML.psd1 -Force

$Url = 'https://example.com'
$OutFile = Join-Path $PSScriptRoot 'outline.json'
Export-HTMLOutline -Url $Url -Path $OutFile
Get-Content -LiteralPath $OutFile
