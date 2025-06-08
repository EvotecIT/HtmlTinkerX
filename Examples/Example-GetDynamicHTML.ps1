Import-Module .\PSParseHTML.psd1 -Force

# Example of using Invoke-HTMLRendering to get dynamic HTML content
$HTML = Invoke-HTMLRendering -Url "https://www.evotec.xyz"
$HTML
# Save HTML to file
Invoke-HTMLRendering -Url "https://www.evotec.xyz" -OutFile "$PSScriptRoot\Output\evotec.html"