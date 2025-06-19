Import-Module .\PSParseHTML.psd1 -Force

$session = Invoke-HTMLRendering -Url 'https://example.com' -Session
Start-HTMLTracing -Session $session

Invoke-HTMLNavigation -Session $session -Url 'https://example.com/profile'

Stop-HTMLTracing -Session $session -OutFile "$PSScriptRoot\Output\trace.zip"
Save-HTMLHar -Session $session -OutFile "$PSScriptRoot\Output\traffic.har"

Close-HTMLSession -Session $session
