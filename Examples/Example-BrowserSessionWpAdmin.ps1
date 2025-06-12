Import-Module .\PSParseHTML.psd1 -Force

# Credentials
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
$session = Open-HTMLSession @invokeHTMLRenderingSplat
# Save screenshot of the page should work with session
Save-HTMLScreenshot -Session $Session -OutFile "$PSScriptRoot\Output\EvotecPageAdmin1.png" -Open
# # We should add new cmdlet that will navigate to the page we tell it to navigate to
Invoke-HTMLNavigation -Session $Session -Url 'https://evotec.xyz/wp-admin/edit.php'
# # Save screenshot of the page should work with session
Save-HTMLScreenshot -Session $Session -OutFile "$PSScriptRoot\Output\EvotecPageAdmin2.png" -Open
# # We should add new cmdlet that will navigate to the page we tell it to navigate to, but also allow Save-HTMLScreenshot to work with session from the page we navigate to
Invoke-HTMLNavigation -Session $Session -Url 'https://evotec.xyz/wp-admin/edit.php' -PassThru | Save-HTMLScreenshot -OutFile "$PSScriptRoot\Output\EvotecPageAdmin3.png" -Open
# # Navigate to plugins page and save screenshot
Invoke-HTMLNavigation -Session $Session -Url 'https://evotec.xyz/wp-admin/edit.php?post_type=page' -PassThru | Save-HTMLScreenshot -OutFile "$PSScriptRoot\Output\EvotecPageAdmin4.png" -Open
# # Navigate to team members page and save screenshot
Invoke-HTMLNavigation -Session $Session -Url 'https://evotec.xyz/wp-admin/edit.php?post_type=thegem_team_person' -PassThru | Save-HTMLScreenshot -OutFile "$PSScriptRoot\Output\EvotecPageAdmin3.png" -Open
# Get interactable elements from the session
Get-HTMLInteractable -Session $Session | Format-Table
# Navigate to profile page, this should error because of multiple elements with the same text
Invoke-HTMLNavigation -Session $Session -Text "Profile" -PassThru | Save-HTMLScreenshot -OutFile "$PSScriptRoot\Output\EvotecPageAdmin5.png" -Open
# Be exact with the text to avoid multiple elements with the same text
Invoke-HTMLNavigation -Session $Session -Text "Profile" -Exact -PassThru | Save-HTMLScreenshot -OutFile "$PSScriptRoot\Output\EvotecPageAdmin5.png" -Open
# Close the session using new cmdlet alias (Stop-HTMLSession)
Close-HTMLSession -Session $Session | Out-Null