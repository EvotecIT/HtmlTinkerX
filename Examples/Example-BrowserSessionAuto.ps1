Import-Module .\PSParseHTML.psd1 -Force

$cred = Get-Credential
$session = Start-HtmlBrowserSession -Url 'https://example.com/login'
Invoke-HtmlBrowserLogin -Session $session -Username $cred.UserName -Password ($cred.GetNetworkCredential().Password) -Timeout 15000
Invoke-HtmlBrowserNavigation -Session $session -Url 'https://example.com/protected'
Save-HtmlBrowserScreenshot -Session $session -OutFile "$PSScriptRoot\Output\secure1.png" -Selector '#content'
Invoke-HtmlBrowserNavigation -Session $session -Url 'https://example.com/downloads' | Save-HtmlBrowserScreenshot -OutFile "$PSScriptRoot\Output\secure2.png"
Save-HtmlBrowserAttachment -Session $session -Path "$PSScriptRoot\Output" -Filter '.pdf'
Close-HtmlBrowserSession -Session $session
