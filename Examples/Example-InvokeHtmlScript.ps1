Import-Module ../PSParseHTML.psd1 -Force

$path = Join-Path $PSScriptRoot '../Tests/Documents/dynamic.html'
$uri = [System.Uri]::new($path).AbsoluteUri
$session = Invoke-HTMLRendering -Url $uri -Session

# Add a new paragraph element
$addScript = 'document.body.insertAdjacentHTML("beforeend", "<p id=\"demo\">Hello</p>");'
Invoke-HTMLScript -Session $session -Script $addScript | Out-Null

# Retrieve the text we just added
$getScript = 'document.getElementById("demo").textContent'
$text = Invoke-HTMLScript -Session $session -Script $getScript
$text
