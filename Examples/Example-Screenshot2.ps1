Import-Module .\PSParseHTML.psd1 -Force

$Credential = Get-Credential
$null = Invoke-HTMLRendering -Url 'https://evotec.xyz/wp-admin/' -Credential $Credential -Browser Chromium
Save-HTMLScreenshot -Url 'https://evotec.xyz/wp-admin/' -OutFile "$PSScriptRoot\Output\WpAdmin.png" -Full -Open -Browser Chromium

