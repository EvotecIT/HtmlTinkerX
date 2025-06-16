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
}
