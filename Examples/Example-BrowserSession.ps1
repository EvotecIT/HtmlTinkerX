Import-Module .\PSParseHTML.psd1 -Force

$cred = Get-Credential
$session = Start-HtmlBrowserSession -Url 'https://example.com/protected' `
    -Credential $cred `
    -LoginUrl 'https://example.com/login' `
    -UsernameSelector 'input[name=user]' `
    -PasswordSelector 'input[name=pass]' `
    -SubmitSelector 'button[type=submit]' `
    -Timeout 15000
Save-HtmlBrowserScreenshot -Session $session -OutFile "$PSScriptRoot\Output\secure1.png" -Selector '#content'
Invoke-HtmlBrowserNavigation -Session $session -Url 'https://example.com/downloads' | Save-HtmlBrowserScreenshot -OutFile "$PSScriptRoot\Output\secure2.png"
Save-HtmlBrowserAttachment -Session $session -Path "$PSScriptRoot\Output" -Filter '.pdf'
Close-HtmlBrowserSession -Session $session
