describe 'Invoke-HTMLScript' {
    it 'Manipulates the DOM and retrieves text' {
        $path = Join-Path $PSScriptRoot 'Documents/dynamic.html'
        $uri = [System.Uri]::new($path).AbsoluteUri
        $session = Invoke-HTMLRendering -Url $uri -Session
        $add = 'document.body.insertAdjacentHTML("beforeend", "<p id=\"new\">Hi</p>");'
        Invoke-HTMLScript -Session $session -Script $add | Out-Null
        $get = 'document.getElementById("new").textContent'
        $text = Invoke-HTMLScript -Session $session -Script $get
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
