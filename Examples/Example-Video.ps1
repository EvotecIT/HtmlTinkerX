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
$session = Start-HtmlBrowserVideoCapture -Session $session -OutFile "$PSScriptRoot\Output\WP1.webm"
# Get interactable elements from the session
Get-HtmlBrowserInteractable -Session $session -Filter "Media" -IncludeHidden | Format-Table
# # We should add new cmdlet that will navigate to the page we tell it to navigate to
Invoke-HTMLNavigation -Session $Session -Url 'https://evotec.xyz/wp-admin/edit.php'
# # We should add new cmdlet that will navigate to the page we tell it to navigate to
Invoke-HTMLNavigation -Session $Session -Url 'https://evotec.xyz/wp-admin/edit.php'
# # Navigate to plugins page and save screenshot
Invoke-HTMLNavigation -Session $Session -Url 'https://evotec.xyz/wp-admin/edit.php?post_type=page'
# # Navigate to team members page and save screenshot
Invoke-HTMLNavigation -Session $Session -Url 'https://evotec.xyz/wp-admin/edit.php?post_type=thegem_team_person'
# Navigate to profile page, this should error because of multiple elements with the same text
Invoke-HTMLNavigation -Session $Session -Text "Profile"
# Be exact with the text to avoid multiple elements with the same text
Invoke-HTMLNavigation -Session $Session -Text "Profile" -Exact
# Close video recording
Stop-HTMLVideoRecording -Session $session
# Close the session using new cmdlet alias (Stop-HTMLSession)
Close-HTMLSession -Session $Session | Out-Null