Import-Module .\PSParseHTML.psd1 -Force

$Credentials = [PSCredential]::new('TestUser', (ConvertTo-SecureString -String $Env:WordpressPassword -AsPlainText -Force))

$sessionParams = @{
    Url              = 'https://evotec.xyz/wp-admin'
    LoginUrl         = 'https://evotec.xyz/wp-login.php'
    UsernameSelector = '#user_login'
    PasswordSelector = '#user_pass'
    SubmitSelector   = '#wp-submit'
    Credential       = $Credentials
    Session          = $true
}

$session = Open-HTMLSession @sessionParams

Save-HTMLScreenshot -Session $session -OutFile "$PSScriptRoot\Output\WP1.png" -Open

$session = Start-HTMLVideoRecording -Session $session -OutFile "$PSScriptRoot\Output\WP1.webm"

Get-HTMLInteractable -Session $session -Filter "Media" -IncludeHidden | Format-Table

Stop-HTMLVideoRecording -Session $session
