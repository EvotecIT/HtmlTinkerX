Import-Module .\PSParseHTML.psd1 -Force

# Example of using Invoke-HtmlRendering to get dynamic HTML content
$HTML = Invoke-HtmlRendering -Url "https://www.evotec.xyz"
$HTML
# Save HTML to file
Invoke-HtmlRendering -Url "https://www.evotec.xyz" -OutFile "$PSScriptRoot\Output\evotec.html"