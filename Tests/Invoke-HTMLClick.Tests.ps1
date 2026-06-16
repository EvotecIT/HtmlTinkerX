Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

describe 'Invoke-HTMLClick' {
    it 'Validates ClickCount range' {
        { Invoke-HTMLClick -Selector '#demo' -ClickCount 0 } | Should -Throw
    }

    it 'Can ignore a missing selector when IfVisible is used' {
        $session = Invoke-HtmlRendering -Url 'about:blank' -Session

        try {
            { Invoke-HtmlClick -Session $session -Selector '#missing' -IfVisible -Timeout 100 } | Should -Not -Throw
        } finally {
            Close-HtmlBrowserSession -Session $session
        }
    }

    it 'Can click a visible text target' {
        $htmlPath = Join-Path $TestDrive 'text-click.html'
        @'
<!doctype html>
<html>
<body>
<button id="reveal" onclick="document.getElementById('result').textContent = 'revealed';">Reveal</button>
<main id="result"></main>
</body>
</html>
'@ | Set-Content -LiteralPath $htmlPath -Encoding UTF8
        $uri = [System.Uri]::new($htmlPath).AbsoluteUri
        $session = Invoke-HtmlRendering -Url $uri -Session

        try {
            Invoke-HtmlClick -Session $session -Text 'Reveal' -Exact
            $text = Get-HtmlContent -Session $session -Selector '#result' -AsText

            $text | Should -Be 'revealed'
        } finally {
            Close-HtmlBrowserSession -Session $session
        }
    }

    it 'Honors Nth for best-effort visible text clicks' {
        $htmlPath = Join-Path $TestDrive 'text-click-nth.html'
        @'
<!doctype html>
<html>
<body>
<button onclick="document.getElementById('result').textContent = 'first';">Choose</button>
<button onclick="document.getElementById('result').textContent = 'second';">Choose</button>
<main id="result"></main>
</body>
</html>
'@ | Set-Content -LiteralPath $htmlPath -Encoding UTF8
        $uri = [System.Uri]::new($htmlPath).AbsoluteUri
        $session = Invoke-HtmlRendering -Url $uri -Session

        try {
            Invoke-HtmlClick -Session $session -Text 'Choose' -Exact -Nth 1 -IfVisible
            $text = Get-HtmlContent -Session $session -Selector '#result' -AsText

            $text | Should -Be 'second'
        } finally {
            Close-HtmlBrowserSession -Session $session
        }
    }

    it 'Rejects selector Nth for best-effort visible clicks' {
        $session = Invoke-HtmlRendering -Url 'about:blank' -Session

        try {
            { Invoke-HtmlClick -Session $session -Selector 'button' -Nth 1 -IfVisible } | Should -Throw '*Nth*IfVisible*selector*'
        } finally {
            Close-HtmlBrowserSession -Session $session
        }
    }
}
