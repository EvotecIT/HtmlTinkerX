Import-Module ..\PSParseHTML.psd1 -Force

$cred = Get-Credential
$session = Invoke-HTMLRendering -Url 'https://example.com/protected' `
    -Credential $cred `
    -LoginUrl 'https://example.com/login' `
    -UsernameSelector 'input[name=user]' `
    -PasswordSelector 'input[name=pass]' `
    -SubmitSelector 'button[type=submit]' `
    -Session
Save-HTMLScreenshot -Session $session -OutFile "$PSScriptRoot\Output\secure.png" -Selector '#content'
# navigate to downloads using the same session
$session.Page.GotoAsync('https://example.com/downloads') | Out-Null
Save-HTMLDownload -Session $session -Path "$PSScriptRoot\Output" -Filter '.pdf'
[PSParseHTML.HtmlBrowserRenderer]::CloseSessionAsync($session).GetAwaiter().GetResult()
