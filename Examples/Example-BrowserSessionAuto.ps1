Import-Module .\PSParseHTML.psd1 -Force

$cred = Get-Credential
$session = Open-HTMLSession -Url 'https://example.com/login' -Session
Invoke-HTMLLogin -Session $session -Username $cred.UserName -Password ($cred.GetNetworkCredential().Password) -Timeout 15000
Invoke-HTMLNavigation -Session $session -Url 'https://example.com/protected'
Save-HTMLScreenshot -Session $session -OutFile "$PSScriptRoot\Output\secure1.png" -Selector '#content'
Invoke-HTMLNavigation -Session $session -Url 'https://example.com/downloads' | Save-HTMLScreenshot -OutFile "$PSScriptRoot\Output\secure2.png"
Save-HTMLAttachment -Session $session -Path "$PSScriptRoot\Output" -Filter '.pdf'
Close-HTMLSession -Session $session
