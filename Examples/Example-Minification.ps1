Import-Module .\PSParseHTML.psd1 -Force

$inputPath = Join-Path $PSScriptRoot 'Input\Minification'
$outputPath = Join-Path $PSScriptRoot 'Output'
New-Item -ItemType Directory -Path $inputPath, $outputPath -Force | Out-Null

$htmlPath = Join-Path $inputPath 'sample.html'
$cssPath = Join-Path $inputPath 'sample.css'
$javascriptPath = Join-Path $inputPath 'sample.js'

Set-Content -LiteralPath $htmlPath -Encoding UTF8 -Value @'
<!doctype html>
<html>
<head>
  <title>Minification sample</title>
  <link rel="stylesheet" href="sample.css">
</head>
<body>
  <main class="content">
    <h1>Mailbox export proof</h1>
    <p>This small local file keeps the example portable.</p>
  </main>
  <script src="sample.js"></script>
</body>
</html>
'@

Set-Content -LiteralPath $cssPath -Encoding UTF8 -Value @'
.content {
    display: grid;
    gap: 0.75rem;
    color: #1f2937;
}
'@

Set-Content -LiteralPath $javascriptPath -Encoding UTF8 -Value @'
const heading = document.querySelector("h1");
if (heading) {
    heading.dataset.ready = "true";
}
'@

Format-HTML -File $htmlPath -OutputFile (Join-Path $outputPath 'Example.Minification.Formatted.html')
Format-JavaScript -File $javascriptPath -OutputFile (Join-Path $outputPath 'Example.Minification.Formatted.js')
Optimize-CSS -File $cssPath -OutputFile (Join-Path $outputPath 'Example.Minification.Optimized.css')
Optimize-JavaScript -File $javascriptPath -OutputFile (Join-Path $outputPath 'Example.Minification.Optimized.js')
Optimize-HTML -File $htmlPath -OutputFile (Join-Path $outputPath 'Example.Minification.Optimized.html')
