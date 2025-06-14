Import-Module ../PSParseHTML.psd1 -Force

$path = Join-Path $PSScriptRoot '../Tests/Documents/dynamic.html'
$uri = [System.Uri]::new($path).AbsoluteUri
$session = Invoke-HTMLRendering -Url $uri -Session

# Add a new paragraph element
Invoke-HTMLScript -Session $session -Script "document.body.insertAdjacentHTML('beforeend','<p id=\"demo\">Hello</p>');" | Out-Null

# Retrieve the text we just added
$text = Invoke-HTMLScript -Session $session -Script "document.getElementById('demo').textContent"
$text
