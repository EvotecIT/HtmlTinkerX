describe 'Invoke-HTMLScript' {
    it 'Manipulates the DOM and retrieves text' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $session = Invoke-HTMLRendering -Url $uri -Session
        Invoke-HTMLScript -Session $session -Script "document.body.insertAdjacentHTML('beforeend','<p id=\"new\">Hi</p>');" | Out-Null
        $text = Invoke-HTMLScript -Session $session -Script "document.getElementById('new').textContent"
        $text | Should -Be 'Hi'
    }

    it 'Returns expression result' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $session = Invoke-HTMLRendering -Url $uri -Session
        $val = Invoke-HTMLScript -Session $session -Script '2 + 3'
        $val | Should -Be 5
    }
}
