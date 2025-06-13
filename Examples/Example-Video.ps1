Import-Module .\PSParseHTML.psd1 -Force

$Credentials = [PSCredential]::new('TestUser', (ConvertTo-SecureString -String $Env:WordpressPassword -AsPlainText -Force))
# Use Invoke-HTMLRendering to create a session, alternatively use Start-HTMLSession, Open-HTMLSession which should be an alias for Invoke-HTMLRendering
$invokeHTMLRenderingSplat = @{
    Url              = 'https://evotec.xyz/wp-admin'
    LoginUrl         = 'https://evotec.xyz/wp-login.php'
    UsernameSelector = '#user_login'
    PasswordSelector = '#user_pass'
    SubmitSelector   = '#wp-submit'
    Credential       = $Credentials
    Session          = $true
}
# When using Session, you can either save $Session variable or use the "default" session
# Default session is always used unless you specify NoSession
$null = Open-HTMLSession @invokeHTMLRenderingSplat

Start-HTMLVideoRecording -OutFile "$PSScriptRoot\Output\WP1.mp4"

Get-HTMLInteractable -Filter "Media" -IncludeHidden | Format-Table

Stop-HTMLVideoRecording -OutFile "$PSScriptRoot\Output\WP1.mp4"