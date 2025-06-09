Import-Module .\PSParseHTML.psd1 -Force

$Session = Start-HTMLSession -Url 'https://evotec.xyz' -Session
Save-HTMLScreenshot -Session $Session -OutFile "$PSScriptRoot\Output\Interactable1.png" -Open
Get-HTMLInteractable -Session $Session | Format-Table

Get-HTMLInteractable -Url 'https://evotec.xyz' | Format-Table

Get-HTMLInteractable -Path "$PSScriptRoot\Input\azure_status.html" | Format-Table