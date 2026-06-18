Import-Module ../PSParseHTML.psd1 -Force

$path = Join-Path $PSScriptRoot '../Tests/Documents/dynamic.html'
$uri = [System.Uri]::new($path).AbsoluteUri
$session = Start-HtmlBrowserSession -Url $uri

# Add a new paragraph element
$addScript = 'document.body.insertAdjacentHTML("beforeend", "<p id=\"demo\">Hello</p>");'
Invoke-HtmlBrowserScript -Session $session -Script $addScript | Out-Null

# Retrieve the text we just added
$getScript = 'document.getElementById("demo").textContent'
$text = Invoke-HtmlBrowserScript -Session $session -Script $getScript
$text
