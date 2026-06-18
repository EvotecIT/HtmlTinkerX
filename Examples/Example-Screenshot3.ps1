Import-Module .\PSParseHTML.psd1 -Force

$Credentials = [PSCredential]::new('TestUser', (ConvertTo-SecureString -String $Env:WordpressPassword -AsPlainText -Force))
$browserSessionSplat = @{
    Url              = 'https://evotec.xyz/wp-admin'
    Browser          = 'Chromium'
    LoginUrl         = 'https://evotec.xyz/wp-login.php'
    UsernameSelector = '#user_login'
    PasswordSelector = '#user_pass'
    SubmitSelector   = '#wp-submit'
    Credential       = $Credentials
}
$Session = Start-HtmlBrowserSession @browserSessionSplat
Save-HtmlBrowserScreenshot -Session $Session -OutFile "$PSScriptRoot\Output\EvotecPageAdmin1.png" -Open
$null = $Session.Page.GotoAsync('https://evotec.xyz/wp-admin/edit.php')
Save-HtmlBrowserScreenshot -Session $Session -OutFile "$PSScriptRoot\Output\EvotecPageAdmin2.png" -Open
$null = Close-HtmlBrowserSession -Session $Session
