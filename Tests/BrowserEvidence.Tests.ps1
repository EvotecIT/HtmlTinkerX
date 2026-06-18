Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

Describe 'Browser evidence exports' {
    It 'exports the evidence command' {
        (Get-Command Export-HtmlBrowserEvidence).Name | Should -Be 'Export-HtmlBrowserEvidence'
        (Get-Command Export-HtmlBrowserEvidence).Parameters.Keys | Should -Contain 'NoRedaction'
        (Get-Command Export-HtmlBrowserEvidence).Parameters.Keys | Should -Contain 'Scenario'
        (Get-Command Export-HtmlBrowserEvidence).Parameters.Keys | Should -Contain 'Proxy'
        (Get-Command Export-HtmlBrowserEvidence).Parameters.Keys | Should -Contain 'ProxyCredential'
        (Get-Command Export-HtmlBrowserEvidence).Parameters.Keys | Should -Contain 'SsoHandoffSummary'
        (Get-Command Export-HtmlBrowserEvidence).Parameters.Keys | Should -Contain 'PreventSsoAutoSubmit'
        (Get-Command Export-HtmlBrowserEvidence).Parameters.Keys | Should -Contain 'NoScreenshotMask'
        (Get-Command Save-HtmlBrowserScreenshot).Parameters.Keys | Should -Contain 'MaskSensitiveElement'
        (Get-Command Save-HtmlBrowserPdf).Parameters.Keys | Should -Contain 'MaskSensitiveElement'
        (Get-Command Export-HtmlBrowserEvidence).Parameters['NoScreenshotMask'].Aliases | Should -Contain 'NoVisualMask'
        (Get-Command Export-HtmlBrowserEvidence).Parameters['ScreenshotMaskSelector'].Aliases | Should -Contain 'VisualMaskSelector'
        (Get-Command Export-HtmlBrowserEvidence).Parameters['ScreenshotMaskColor'].Aliases | Should -Contain 'VisualMaskColor'
    }

    It 'rejects document resource blocking for one-shot evidence navigation' {
        $pagePath = Join-Path $TestDrive 'document-block-evidence.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value '<!doctype html><main>proof</main>'
        $outFolder = Join-Path $TestDrive 'document-block-evidence'

        { Export-HtmlBrowserEvidence -Path $pagePath -OutFolder $outFolder -BlockResourceType Document } |
            Should -Throw -ExpectedMessage '*BlockResourceType Document would abort page navigation*'
    }

    It 'exports a one-shot evidence pack with a manifest and hashes' {
        $pagePath = Join-Path $TestDrive 'evidence-page.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value @'
<!doctype html>
<html>
<head><title>Evidence Title</title></head>
<body>
  <main>
    <h1>Visible Proof</h1>
    <p>Mailbox export confirmation is visible.</p>
    <p>Authorization: Bearer abc.def.ghi</p>
    <input type="hidden" name="access_token" value="super-secret-token" />
    <script>window.config = { api_key: "super-secret-api-key" };</script>
  </main>
</body>
</html>
'@
        $outFolder = Join-Path $TestDrive 'evidence-pack'

        $result = Export-HtmlBrowserEvidence -Path $pagePath -OutFolder $outFolder -BaseFileName proof -Scenario AuditProof -Screenshot -Html -VisibleText -Markdown -NetworkSummary -LoadState DomContentLoaded
        $manifest = Get-Content -LiteralPath $result.ManifestPath -Raw | ConvertFrom-Json

        Test-Path -LiteralPath (Join-Path $outFolder 'proof.png') | Should -BeTrue
        Test-Path -LiteralPath (Join-Path $outFolder 'proof.html') | Should -BeTrue
        Test-Path -LiteralPath (Join-Path $outFolder 'proof.txt') | Should -BeTrue
        Test-Path -LiteralPath (Join-Path $outFolder 'proof.md') | Should -BeTrue
        Test-Path -LiteralPath (Join-Path $outFolder 'network-summary.json') | Should -BeTrue
        Test-Path -LiteralPath (Join-Path $outFolder 'evidence-manifest.json') | Should -BeTrue

        $manifest.Title | Should -Be 'Evidence Title'
        $manifest.Redacted | Should -BeTrue
        $manifest.Artifacts.Kind | Should -Contain 'Screenshot'
        $manifest.Artifacts.Kind | Should -Contain 'Html'
        $manifest.Artifacts.Kind | Should -Contain 'Text'
        $manifest.Artifacts.Kind | Should -Contain 'Markdown'
        $manifest.Artifacts.Kind | Should -Contain 'NetworkSummary'
        $manifest.Artifacts | ForEach-Object {
            $_.Sha256 | Should -Match '^[a-f0-9]{64}$'
            $_.SizeBytes | Should -BeGreaterThan 0
        }
        $text = Get-Content -LiteralPath (Join-Path $outFolder 'proof.txt') -Raw
        $html = Get-Content -LiteralPath (Join-Path $outFolder 'proof.html') -Raw
        $markdown = Get-Content -LiteralPath (Join-Path $outFolder 'proof.md') -Raw
        $text | Should -Match 'Visible Proof'
        $text | Should -Not -Match 'abc\.def\.ghi'
        $html | Should -Not -Match 'super-secret-token'
        $html | Should -Not -Match 'super-secret-api-key'
        $html | Should -Match '<redacted>'
        $markdown | Should -Not -Match 'abc\.def\.ghi'
    }

    It 'masks sensitive fields in screenshot evidence by default' {
        $pagePath = Join-Path $TestDrive 'masked-screenshot-page.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value @'
<!doctype html>
<html>
<head>
  <title>Masked Screenshot</title>
  <style>
    body { margin: 0; background: #ffffff; font-family: sans-serif; }
    input { margin: 40px; width: 220px; height: 48px; font-size: 24px; border: 2px solid #111111; }
  </style>
</head>
<body>
  <input id="password" name="password" type="text" value="visible-secret" />
</body>
</html>
'@
        $outFolder = Join-Path $TestDrive 'masked-screenshot'

        Export-HtmlBrowserEvidence -Path $pagePath -OutFolder $outFolder -BaseFileName masked -Artifact Screenshot -ScreenshotMaskColor '#00ff00' -LoadState DomContentLoaded | Out-Null

        Add-Type -AssemblyName System.Drawing
        $bitmap = [System.Drawing.Bitmap]::new((Join-Path $outFolder 'masked.png'))
        try {
            $greenPixels = 0
            for ($x = 0; $x -lt $bitmap.Width; $x++) {
                for ($y = 0; $y -lt $bitmap.Height; $y++) {
                    $pixel = $bitmap.GetPixel($x, $y)
                    if ($pixel.R -lt 20 -and $pixel.G -gt 220 -and $pixel.B -lt 20) {
                        $greenPixels++
                    }
                }
            }

            $greenPixels | Should -BeGreaterThan 100
        } finally {
            $bitmap.Dispose()
        }
    }

    It 'masks PDF evidence without leaving temporary page attributes' {
        $pagePath = Join-Path $TestDrive 'masked-pdf-page.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value @'
<!doctype html>
<html>
<head>
  <title>Masked PDF</title>
  <style>
    body { margin: 24px; background: #ffffff; font-family: sans-serif; }
    #secret { display: inline-block; padding: 8px 12px; color: rgb(180, 0, 0); }
  </style>
</head>
<body>
  <main>
    <h1>Printable proof</h1>
    <span id="secret">visible-pdf-secret</span>
  </main>
</body>
</html>
'@
        $uri = [System.Uri]::new($pagePath).AbsoluteUri
        $outFolder = Join-Path $TestDrive 'masked-pdf'

        $session = Start-HtmlBrowserSession -Url $uri -LoadState DomContentLoaded
        try {
            $result = Export-HtmlBrowserEvidence -Session $session -OutFolder $outFolder -BaseFileName masked -Artifact Pdf -VisualMaskSelector '#secret' -VisualMaskColor '#00ff00'
            $maskCount = Invoke-HtmlBrowserScript -Session $session -Script "() => document.querySelectorAll('[data-htmltinkerx-visual-mask]').length"
            $inlineStyle = Invoke-HtmlBrowserScript -Session $session -Script "() => document.querySelector('#secret').getAttribute('style') || ''"

            Test-Path -LiteralPath (Join-Path $outFolder 'masked.pdf') | Should -BeTrue
            $result.Artifacts.Kind | Should -Contain 'Pdf'
            [int]$maskCount | Should -Be 0
            [string]$inlineStyle | Should -Not -Match 'background-color'
        } finally {
            Close-HtmlBrowserSession -Session $session
        }
    }

    It 'exports a redacted SSO handoff summary artifact' {
        $pagePath = Join-Path $TestDrive 'sso-evidence-page.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value @'
<!doctype html>
<html>
<head><title>SSO Evidence</title></head>
<body>
  <form id="handoff" method="post" action="https://service-provider.example/saml/consume">
    <input type="hidden" name="SAMLResponse" value="sso-proof-secret" />
    <input type="hidden" name="RelayState" value="relay-proof-secret" />
    <input type="hidden" name="display" value="proof" />
  </form>
</body>
</html>
'@
        $outFolder = Join-Path $TestDrive 'sso-evidence'

        $result = Export-HtmlBrowserEvidence -Path $pagePath -OutFolder $outFolder -BaseFileName sso -Artifact SsoHandoffSummary -LoadState DomContentLoaded
        $summaryPath = Join-Path $outFolder 'sso-handoff-summary.json'
        $summary = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
        $manifest = Get-Content -LiteralPath $result.ManifestPath -Raw | ConvertFrom-Json

        $result.SsoHandoffCount | Should -Be 1
        $result.Artifacts.Kind | Should -Contain 'SsoHandoffSummary'
        $manifest.SsoHandoffCount | Should -Be 1
        $manifest.Artifacts.Kind | Should -Contain 'SsoHandoffSummary'
        $summary.Count | Should -Be 1
        $summary.Redacted | Should -BeTrue
        $summary.Handoffs[0].Kind | Should -Be 'Saml'
        ($summary.Handoffs[0].Fields | Where-Object Name -eq 'SAMLResponse').Value | Should -Be '<redacted>'
        ($summary.Handoffs[0].Fields | Where-Object Name -eq 'RelayState').Value | Should -Be '<redacted>'
        ($summary.Handoffs[0].Fields | Where-Object Name -eq 'display').Value | Should -Be 'proof'
        Get-Content -LiteralPath $summaryPath -Raw | Should -Not -Match 'sso-proof-secret'
    }

    It 'holds auto-submitted SSO handoffs for one-shot evidence summaries' {
        $submitPath = Join-Path $TestDrive 'sso-evidence-auto-submit.html'
        $targetPath = Join-Path $TestDrive 'sso-evidence-after-submit.html'
        Set-Content -LiteralPath $targetPath -Encoding UTF8 -Value '<!doctype html><title>After SSO</title><main>Already submitted</main>'
        Set-Content -LiteralPath $submitPath -Encoding UTF8 -Value @"
<!doctype html>
<html>
<head><title>Auto SSO Evidence</title></head>
<body>
  <form id="auto" method="get" action="$([System.Uri]::new($targetPath).AbsoluteUri)">
    <input type="hidden" name="SAMLResponse" value="auto-sso-proof-secret" />
    <input type="hidden" name="RelayState" value="auto-relay-proof-secret" />
    <input type="hidden" name="display" value="proof" />
  </form>
  <script>document.getElementById('auto').submit();</script>
</body>
</html>
"@
        $outFolder = Join-Path $TestDrive 'sso-auto-evidence'

        $result = Export-HtmlBrowserEvidence -Path $submitPath -OutFolder $outFolder -BaseFileName auto -Artifact SsoHandoffSummary -LoadState DomContentLoaded
        $summaryPath = Join-Path $outFolder 'sso-handoff-summary.json'
        $summary = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json

        $result.SsoHandoffCount | Should -Be 1
        $summary.Handoffs[0].PageUrl | Should -Match 'sso-evidence-auto-submit\.html'
        $summary.Handoffs[0].AutoSubmitPrevented | Should -BeTrue
        ($summary.Handoffs[0].Fields | Where-Object Name -eq 'SAMLResponse').Value | Should -Be '<redacted>'
        ($summary.Handoffs[0].Fields | Where-Object Name -eq 'RelayState').Value | Should -Be '<redacted>'
        ($summary.Handoffs[0].Fields | Where-Object Name -eq 'display').Value | Should -Be 'proof'
        Get-Content -LiteralPath $summaryPath -Raw | Should -Not -Match 'auto-sso-proof-secret'
    }

    It 'can export raw evidence when redaction is explicitly disabled' {
        $pagePath = Join-Path $TestDrive 'raw-evidence-page.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value '<!doctype html><title>Raw Evidence</title><main><input type="hidden" name="access_token" value="raw-token" /></main>'
        $outFolder = Join-Path $TestDrive 'raw-evidence'

        $result = Export-HtmlBrowserEvidence -Path $pagePath -OutFolder $outFolder -BaseFileName raw -Artifact Html -NoRedaction -LoadState DomContentLoaded

        $result.Redacted | Should -BeFalse
        Get-Content -LiteralPath (Join-Path $outFolder 'raw.html') -Raw | Should -Match 'raw-token'
    }

    It 'exports evidence from a piped browser session' {
        $pagePath = Join-Path $TestDrive 'session-page.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value '<!doctype html><title>Session Evidence</title><main>Session proof text</main>'
        $uri = [System.Uri]::new($pagePath).AbsoluteUri
        $outFolder = Join-Path $TestDrive 'session-evidence'

        $session = Start-HtmlBrowserSession -Url $uri -LoadState DomContentLoaded
        try {
            $result = $session | Export-HtmlBrowserEvidence -OutFolder $outFolder -BaseFileName session -Artifact Html,Text -NoManifest

            $result.ManifestPath | Should -BeNullOrEmpty
            $result.Artifacts.Kind | Should -Contain 'Html'
            $result.Artifacts.Kind | Should -Contain 'Text'
            $result.Artifacts.Kind | Should -Not -Contain 'Screenshot'
            $result.Artifacts.Kind | Should -Not -Contain 'Markdown'
            Test-Path -LiteralPath (Join-Path $outFolder 'session.html') | Should -BeTrue
            Test-Path -LiteralPath (Join-Path $outFolder 'session.txt') | Should -BeTrue
            Test-Path -LiteralPath (Join-Path $outFolder 'session.png') | Should -BeFalse
            Test-Path -LiteralPath (Join-Path $outFolder 'session.md') | Should -BeFalse
            Test-Path -LiteralPath (Join-Path $outFolder 'evidence-manifest.json') | Should -BeFalse
        } finally {
            Close-HtmlBrowserSession -Session $session
        }
    }
}
