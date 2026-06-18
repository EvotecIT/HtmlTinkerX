Import-Module .\PSParseHTML.psd1 -Force

$Credential = Get-Credential
$null = Invoke-HtmlRendering -Url 'https://evotec.xyz/wp-admin/' -Credential $Credential -Browser Chromium
Save-HtmlBrowserScreenshot -Url 'https://evotec.xyz/wp-admin/' -OutFile "$PSScriptRoot\Output\WpAdmin.png" -Full -Open -Browser Chromium

