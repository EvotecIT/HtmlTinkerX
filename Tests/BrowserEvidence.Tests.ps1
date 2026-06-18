Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

if (-not ('PngEvidenceTestReader' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.IO.Compression;
using System.Text;

public static class PngEvidenceTestReader {
    public static int CountGreenPixels(string path) {
        byte[] bytes = File.ReadAllBytes(path);
        int width = 0;
        int height = 0;
        int bitDepth = 0;
        int colorType = 0;
        MemoryStream compressed = new MemoryStream();
        int offset = 8;
        while (offset < bytes.Length) {
            int length = ReadInt32BigEndian(bytes, offset);
            string type = Encoding.ASCII.GetString(bytes, offset + 4, 4);
            int dataOffset = offset + 8;
            if (type == "IHDR") {
                width = ReadInt32BigEndian(bytes, dataOffset);
                height = ReadInt32BigEndian(bytes, dataOffset + 4);
                bitDepth = bytes[dataOffset + 8];
                colorType = bytes[dataOffset + 9];
            } else if (type == "IDAT") {
                compressed.Write(bytes, dataOffset, length);
            } else if (type == "IEND") {
                break;
            }

            offset = dataOffset + length + 4;
        }

        if (bitDepth != 8 || (colorType != 2 && colorType != 6)) {
            throw new InvalidOperationException("Test PNG reader supports only 8-bit RGB/RGBA PNG files.");
        }

        byte[] zlib = compressed.ToArray();
        MemoryStream deflateInput = new MemoryStream(zlib, 2, zlib.Length - 6);
        DeflateStream deflate = new DeflateStream(deflateInput, CompressionMode.Decompress);
        MemoryStream raw = new MemoryStream();
        try {
            deflate.CopyTo(raw);
        } finally {
            deflate.Dispose();
            deflateInput.Dispose();
            compressed.Dispose();
        }

        byte[] data = raw.ToArray();
        int bytesPerPixel = colorType == 6 ? 4 : 3;
        int rowLength = width * bytesPerPixel;
        byte[] previous = new byte[rowLength];
        int source = 0;
        int greenPixels = 0;

        for (int y = 0; y < height; y++) {
            int filter = data[source++];
            byte[] row = new byte[rowLength];
            Array.Copy(data, source, row, 0, rowLength);
            source += rowLength;
            ApplyFilter(row, previous, bytesPerPixel, filter);

            for (int x = 0; x < width; x++) {
                int pixel = x * bytesPerPixel;
                if (row[pixel] < 20 && row[pixel + 1] > 220 && row[pixel + 2] < 20) {
                    greenPixels++;
                }
            }

            previous = row;
        }

        return greenPixels;
    }

    private static void ApplyFilter(byte[] row, byte[] previous, int bytesPerPixel, int filter) {
        for (int i = 0; i < row.Length; i++) {
            int left = i >= bytesPerPixel ? row[i - bytesPerPixel] : 0;
            int up = previous[i];
            int upLeft = i >= bytesPerPixel ? previous[i - bytesPerPixel] : 0;
            int add;
            switch (filter) {
                case 0:
                    add = 0;
                    break;
                case 1:
                    add = left;
                    break;
                case 2:
                    add = up;
                    break;
                case 3:
                    add = (left + up) / 2;
                    break;
                case 4:
                    add = Paeth(left, up, upLeft);
                    break;
                default:
                    throw new InvalidOperationException("Unsupported PNG filter.");
            }
            row[i] = unchecked((byte)(row[i] + add));
        }
    }

    private static int Paeth(int left, int up, int upLeft) {
        int p = left + up - upLeft;
        int pa = Math.Abs(p - left);
        int pb = Math.Abs(p - up);
        int pc = Math.Abs(p - upLeft);
        if (pa <= pb && pa <= pc) return left;
        if (pb <= pc) return up;
        return upLeft;
    }

    private static int ReadInt32BigEndian(byte[] bytes, int offset) {
        return (bytes[offset] << 24)
            | (bytes[offset + 1] << 16)
            | (bytes[offset + 2] << 8)
            | bytes[offset + 3];
    }
}
'@
}

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

    It 'redacts OAuth state URLs and MFA values from evidence artifacts' {
        $pagePath = Join-Path $TestDrive 'evidence-sensitive-callback.html'
        Set-Content -LiteralPath $pagePath -Encoding UTF8 -Value @'
<!doctype html>
<html>
<head><title>Sensitive Callback</title></head>
<body>
  <main>
    <p>otp=123456 passcode=654321 pin=4321</p>
    <input type="hidden" name="otp" value="111222" />
    <input type="hidden" name="pwd" value="temporary-password" />
    <script>window.mfa = { otp: "333444", passcode: "555666", pwd: "script-password" };</script>
  </main>
</body>
</html>
'@
        $uri = [System.Uri]::new($pagePath).AbsoluteUri + '#/callback?code=secret-code&state=secret-state'
        $outFolder = Join-Path $TestDrive 'sensitive-callback-evidence'
        $session = Start-HtmlBrowserSession -Url $uri -LoadState DomContentLoaded

        try {
            $result = Export-HtmlBrowserEvidence -Session $session -OutFolder $outFolder -BaseFileName callback -Artifact Html,Text,Markdown -NetworkSummary
        } finally {
            Close-HtmlBrowserSession -Session $session
        }

        $manifest = Get-Content -LiteralPath $result.ManifestPath -Raw | ConvertFrom-Json
        $html = Get-Content -LiteralPath (Join-Path $outFolder 'callback.html') -Raw
        $text = Get-Content -LiteralPath (Join-Path $outFolder 'callback.txt') -Raw
        $markdown = Get-Content -LiteralPath (Join-Path $outFolder 'callback.md') -Raw

        $manifest.Url | Should -Match 'state=<redacted>'
        $manifest.FinalUrl | Should -Match 'code=<redacted>'
        $manifest.Url | Should -Not -Match 'secret-state'
        $manifest.FinalUrl | Should -Not -Match 'secret-code'
        $html | Should -Not -Match '111222|temporary-password|333444|555666|script-password'
        $text | Should -Not -Match '123456|654321|4321'
        $markdown | Should -Not -Match '123456|654321|4321'
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

        [PngEvidenceTestReader]::CountGreenPixels((Join-Path $outFolder 'masked.png')) | Should -BeGreaterThan 100
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
