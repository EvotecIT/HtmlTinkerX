Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force
. "$PSScriptRoot/Support/HtmlRedirectTestServer.ps1"

Describe 'ConvertTo-HtmlDatasetJsonL' {
    It 'exports the single-page dataset JSONL command' {
        Get-Command ConvertTo-HtmlDatasetJsonL | Should -Not -BeNullOrEmpty
    }

    It 'converts a workbench result to JSONL with provenance and redaction hints' {
        $html = @'
<html>
<head>
<title>Dataset Demo</title>
<meta property="og:title" content="Dataset Demo" />
<script>fetch("/api/items");</script>
</head>
<body>
<main>
<h1>Dataset Demo</h1>
<p>This page is small but still becomes one dataset record with useful provenance and redaction metadata.</p>
<form method="post" action="/submit"><input type="hidden" name="csrf" value="secret" /><input name="user" /></form>
</main>
</body>
</html>
'@

        $jsonl = Invoke-HtmlPageWorkbench -Content $html -BaseUrl 'https://example.org/dataset' |
            ConvertTo-HtmlDatasetJsonL -MaxChunkWords 50
        $record = $jsonl | ConvertFrom-Json

        $record.ChunkId | Should -Be 'page-chunk-0001'
        $record.SourceUrl | Should -Be 'https://example.org/dataset'
        $record.Title | Should -Be 'Dataset Demo'
        $record.Text | Should -Match 'dataset record'
        $record.DataKinds | Should -Contain 'OpenGraph'
        $record.FormCount | Should -Be 1
        $record.EndpointCount | Should -BeGreaterThan 0
        $record.RedactionHints | Should -Contain 'hidden-form-fields'
        $record.Provenance.Kind | Should -Contain 'ReadableText'
        $record.Provenance.Kind | Should -Contain 'Endpoint'
    }

    It 'can return dataset chunk objects directly' {
        $html = '<html><body><main><h1>Object Output</h1><p>Dataset object output works without a JSON conversion roundtrip.</p></main></body></html>'

        $chunk = ConvertTo-HtmlDatasetJsonL -Content $html -BaseUrl 'https://example.org/object' -AsObject

        $chunk.GetType().Name | Should -Be 'HtmlPageDatasetChunk'
        $chunk.SourceUrl | Should -Be 'https://example.org/object'
        $chunk.Markdown | Should -Match 'Object Output'
    }

    It 'uses the final response Url as the dataset source when Url follows redirects' {
        $server = [HtmlRedirectTestServer]::new()
        try {
            $chunk = ConvertTo-HtmlDatasetJsonL -Url ($server.Url + 'redirect-dataset') -AsObject

            $chunk.SourceUrl | Should -Be ($server.Url + 'final/dataset')
            $chunk.FinalUrl | Should -Be ($server.Url + 'final/dataset')
        } finally {
            $server.Dispose()
        }
    }
}
