Import-Module .\PSParseHTML.psd1 -Force

$session = Start-HtmlBrowserSession -Url 'https://example.com'
Start-HtmlBrowserTracing -Session $session

Invoke-HtmlBrowserNavigation -Session $session -Url 'https://example.com/profile'

Stop-HtmlBrowserTracing -Session $session -OutFile "$PSScriptRoot\Output\trace.zip"
Export-HtmlBrowserHar -Session $session -OutFile "$PSScriptRoot\Output\traffic.har"

Close-HtmlBrowserSession -Session $session
