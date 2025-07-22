Import-Module ./PSParseHTML.psd1 -Force

$uri = 'https://example.com'
$out = Join-Path $PSScriptRoot 'Output\sample.webm'

$session = Start-HTMLRecording -Url $uri -OutFile $out
Invoke-HTMLNavigation -Session $session -Url $uri
$path = Stop-HTMLRecording -Session $session

Write-Host "Video saved to $path"
