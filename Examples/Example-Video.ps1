Import-Module .\PSParseHTML.psd1 -Force

$Credentials = [PSCredential]::new('TestUser', (ConvertTo-SecureString -String $Env:WordpressPassword -AsPlainText -Force))

$sessionParams = @{
    Url              = 'https://evotec.xyz/wp-admin'
    LoginUrl         = 'https://evotec.xyz/wp-login.php'
    UsernameSelector = '#user_login'
    PasswordSelector = '#user_pass'
    SubmitSelector   = '#wp-submit'
    Credential       = $Credentials
}

$session = Start-HtmlBrowserSession @sessionParams
$session = Start-HtmlBrowserVideoCapture -Session $session -OutFile "$PSScriptRoot\Output\WP1.webm"
# Get interactable elements from the session
Get-HtmlBrowserInteractable -Session $session -Filter "Media" -IncludeHidden | Format-Table
# # We should add new cmdlet that will navigate to the page we tell it to navigate to
Invoke-HtmlBrowserNavigation -Session $Session -Url 'https://evotec.xyz/wp-admin/edit.php'
# # We should add new cmdlet that will navigate to the page we tell it to navigate to
Invoke-HtmlBrowserNavigation -Session $Session -Url 'https://evotec.xyz/wp-admin/edit.php'
# # Navigate to plugins page and save screenshot
Invoke-HtmlBrowserNavigation -Session $Session -Url 'https://evotec.xyz/wp-admin/edit.php?post_type=page'
# # Navigate to team members page and save screenshot
Invoke-HtmlBrowserNavigation -Session $Session -Url 'https://evotec.xyz/wp-admin/edit.php?post_type=thegem_team_person'
# Navigate to profile page, this should error because of multiple elements with the same text
Invoke-HtmlBrowserNavigation -Session $Session -Text "Profile"
# Be exact with the text to avoid multiple elements with the same text
Invoke-HtmlBrowserNavigation -Session $Session -Text "Profile" -Exact
# Close video recording
Stop-HtmlBrowserVideoCapture -Session $session
# Close the session using new cmdlet alias (Close-HtmlBrowserSession)
Close-HtmlBrowserSession -Session $Session | Out-Null
