Import-Module ./PSParseHTML.psd1 -Force

$Content = @'
<h1>Good</h1>
<hX>Bad</hX>
<h2>Also Good</h2>
'@

$OutFile = Join-Path $PSScriptRoot 'outline-malformed.json'
Export-HTMLOutline -Content $Content -Path $OutFile
Get-Content -LiteralPath $OutFile
