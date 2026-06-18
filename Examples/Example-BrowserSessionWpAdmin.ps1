Import-Module .\PSParseHTML.psd1 -Force

# Credentials
$Credentials = [PSCredential]::new('TestUser', (ConvertTo-SecureString -String $Env:WordpressPassword -AsPlainText -Force))
# Start a reusable browser session with the canonical session cmdlet.
$browserSessionSplat = @{
    Url              = 'https://evotec.xyz/wp-admin'
    LoginUrl         = 'https://evotec.xyz/wp-login.php'
    UsernameSelector = '#user_login'
    PasswordSelector = '#user_pass'
    SubmitSelector   = '#wp-submit'
    Credential       = $Credentials
}
$session = Start-HtmlBrowserSession @browserSessionSplat
# Save screenshot of the page should work with session
Save-HtmlBrowserScreenshot -Session $Session -OutFile "$PSScriptRoot\Output\EvotecPageAdmin1.png" -Open
# Navigate to a page inside the current session
Invoke-HtmlBrowserNavigation -Session $Session -Url 'https://evotec.xyz/wp-admin/edit.php'
# Save screenshot of the page after navigation
Save-HtmlBrowserScreenshot -Session $Session -OutFile "$PSScriptRoot\Output\EvotecPageAdmin2.png" -Open
# Pipe the navigated page into screenshot capture
Invoke-HtmlBrowserNavigation -Session $Session -Url 'https://evotec.xyz/wp-admin/edit.php' -PassThru | Save-HtmlBrowserScreenshot -OutFile "$PSScriptRoot\Output\EvotecPageAdmin3.png" -Open
# Navigate to a page and save a screenshot
Invoke-HtmlBrowserNavigation -Session $Session -Url 'https://evotec.xyz/wp-admin/edit.php?post_type=page' -PassThru | Save-HtmlBrowserScreenshot -OutFile "$PSScriptRoot\Output\EvotecPageAdmin4.png" -Open
# Navigate to team members page and save a screenshot
Invoke-HtmlBrowserNavigation -Session $Session -Url 'https://evotec.xyz/wp-admin/edit.php?post_type=thegem_team_person' -PassThru | Save-HtmlBrowserScreenshot -OutFile "$PSScriptRoot\Output\EvotecPageAdmin3.png" -Open
# Get interactable elements from the session
Get-HtmlBrowserInteractable -Session $Session | Format-Table
# Navigate to profile page, this should error because of multiple elements with the same text
Invoke-HtmlBrowserNavigation -Session $Session -Text "Profile" -PassThru | Save-HtmlBrowserScreenshot -OutFile "$PSScriptRoot\Output\EvotecPageAdmin5.png" -Open
# Be exact with the text to avoid multiple elements with the same text
Invoke-HtmlBrowserNavigation -Session $Session -Text "Profile" -Exact -PassThru | Save-HtmlBrowserScreenshot -OutFile "$PSScriptRoot\Output\EvotecPageAdmin5.png" -Open
# Close the session
Close-HtmlBrowserSession -Session $Session | Out-Null
