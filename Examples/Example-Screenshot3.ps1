Import-Module .\PSParseHTML.psd1 -Force

$Credentials = [PSCredential]::new('TestUser', (ConvertTo-SecureString -String $Env:WordpressPassword -AsPlainText -Force))
$invokeHTMLRenderingSplat = @{
    Url              = 'https://evotec.xyz/wp-admin'
    Browser          = 'Chromium'
    LoginUrl         = 'https://evotec.xyz/wp-login.php'
    UsernameSelector = '#user_login'
    PasswordSelector = '#user_pass'
    SubmitSelector   = '#wp-submit'
    Credential       = $Credentials
    Session          = $true
}
$Session = Invoke-HTMLRendering @invokeHTMLRenderingSplat
Save-HTMLScreenshot -Session $Session -OutFile "$PSScriptRoot\Output\EvotecPageAdmin1.png" -Open
$null = $Session.Page.GotoAsync('https://evotec.xyz/wp-admin/edit.php')
Save-HTMLScreenshot -Session $Session -OutFile "$PSScriptRoot\Output\EvotecPageAdmin2.png" -Open
$null = Close-HTMLSession -Session $Session
