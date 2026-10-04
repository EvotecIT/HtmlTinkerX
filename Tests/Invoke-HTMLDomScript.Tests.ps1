BeforeAll {
    $script:DomModulePath = Join-Path (Split-Path $PSScriptRoot -Parent) 'PSParseHTML.psd1'
    Import-Module $script:DomModulePath -Force -ErrorAction Stop
}

describe 'Invoke-HTMLDomScript' {
    it 'Executes script against file content' {
        $path = Join-Path $PSScriptRoot 'Documents/sample_form.html'
        $count = Invoke-HTMLDomScript -Path $path -Script 'document.querySelectorAll("form").length'
        $count | Should -Be 2
    }

    it 'Works with direct content' {
        $html = '<div id="demo">Hello</div>'
        $result = Invoke-HTMLDomScript -Content $html -Script 'document.getElementById("demo").textContent'
        $result | Should -Be 'Hello'
    }

    it 'Skips page scripts while retaining the requested DOM expression' {
        $html = '<p id="value">original</p><script>document.getElementById("value").textContent = "page";</script>'
        Invoke-HtmlBrowserDomScript -Content $html -Script 'document.getElementById("value").textContent' | Should -Be 'page'
        Invoke-HtmlBrowserDomScript -Content $html -Script 'document.getElementById("value").textContent' -SkipPageScripts | Should -Be 'original'
    }

    it 'Applies the statement limit before inline page execution' {
        $html = '<script>for(var i=0;i<1000;i++){document.title="work";}</script>'
        { Invoke-HtmlBrowserDomScript -Content $html -Script '1' -MaximumStatements 64 -ErrorAction Stop } | Should -Throw '*statements*'
    }

    it 'Interrupts unbounded execution at the operation timeout' {
        $failure = { Invoke-HtmlBrowserDomScript -Content '<html></html>' -Script 'while(true){}' -Timeout 100 -MaximumStatements ([int]::MaxValue) -ErrorAction Stop } | Should -Throw -PassThru
        $failure.Exception | Should -BeOfType ([TimeoutException])
        Invoke-HtmlBrowserDomScript -Content '<html></html>' -Script '1 + 2' | Should -Be 3
    }

    it 'Bounds content, requested scripts and decoded file input' {
        { Invoke-HtmlBrowserDomScript -Content '<p>large</p>' -Script '1' -MaximumHtmlCharacters 3 -ErrorAction Stop } | Should -Throw '*character limit*'
        { Invoke-HtmlBrowserDomScript -Content '<p></p>' -Script '1 + 2' -MaximumScriptCharacters 3 -ErrorAction Stop } | Should -Throw '*character limit*'
        $path = Join-Path $TestDrive 'bounded.html'
        $html = '<p>Zażółć</p>'
        [IO.File]::WriteAllText($path, $html, [Text.Encoding]::Unicode)
        Invoke-HtmlBrowserDomScript -Path $path -Script 'document.querySelector("p").textContent' -MaximumHtmlCharacters $html.Length | Should -Be 'Zażółć'
        { Invoke-HtmlBrowserDomScript -Path $path -Script '1' -MaximumHtmlCharacters ($html.Length - 1) -ErrorAction Stop } | Should -Throw '*character limit*'
    }

    it 'Propagates pipeline Stop to an executing DOM script' {
        $signal = New-Object Threading.AutoResetEvent($false)
        $worker = [PowerShell]::Create()
        try {
            $null = $worker.AddScript({
                param($modulePath, $signal)
                $env:PSPARSEHTML_DEVELOPMENT_CONFIGURATION = 'Release'
                Import-Module $modulePath -Force -ErrorAction Stop
                $null = Invoke-HtmlBrowserDomScript -Content '<p>Warm</p>' -Script '1'
                $null = $signal.Set()
                Invoke-HtmlBrowserDomScript -Content '<p>Stop</p>' -Script 'while(true){}' -Timeout 8000 -MaximumStatements ([int]::MaxValue) -ErrorAction Stop
            }).AddArgument($script:DomModulePath).AddArgument($signal)
            $invocation = $worker.BeginInvoke()
            $signal.WaitOne(10000) | Should -BeTrue
            Start-Sleep -Milliseconds 100
            $stopwatch = [Diagnostics.Stopwatch]::StartNew()
            $worker.Stop()
            $stopwatch.Stop()
            $stopwatch.ElapsedMilliseconds | Should -BeLessThan 3000
            $worker.InvocationStateInfo.State | Should -Be ([Management.Automation.PSInvocationState]::Stopped)
            try { $null = $worker.EndInvoke($invocation) } catch [Management.Automation.PipelineStoppedException] { }
        } finally {
            $worker.Dispose()
            $signal.Dispose()
        }
    }
}
