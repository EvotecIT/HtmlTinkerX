Import-Module .\PSParseHTML.psd1 -Force

$cred = Get-Credential
$session = Start-HTMLSession -Url 'https://example.com/protected' `
    -Credential $cred `
    -LoginUrl 'https://example.com/login' `
    -UsernameSelector 'input[name=user]' `
    -PasswordSelector 'input[name=pass]' `
    -SubmitSelector 'button[type=submit]' `
    -Timeout 15000
Save-HTMLScreenshot -Session $session -OutFile "$PSScriptRoot\Output\secure1.png" -Selector '#content'
Invoke-HTMLNavigation -Session $session -Url 'https://example.com/downloads' | Save-HTMLScreenshot -OutFile "$PSScriptRoot\Output\secure2.png"
Save-HTMLAttachment -Session $session -Path "$PSScriptRoot\Output" -Filter '.pdf'
Close-HTMLSession -Session $session
