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
$Session = Open-HTMLSession @invokeHTMLRenderingSplat

Get-HTMLInteractable -Session $Session -Filter "Media" | Format-Table


return

<#
Index Text           Tag Selector                                            Id Class                                                          Href
----- ----           --- --------                                            -- -----                                                          ----
   10 Media          a   a[href="upload.php"]                                   wp-has-submenu wp-not-current-submenu menu-top menu-icon-media upload.php
   12 Add Media File a   a[href="media-new.php"]                                                                                               media-new.php
   51 Media          a   a[href="https://evotec.xyz/wp-admin/media-new.php"]    ab-item                                                        https://evotec.xyz/wp-admin/media-new.php
#>

# This will fail because there are multiple elements with the same text
Invoke-HTMLNavigation -Session $Session -Text "Media"

Get-HTMLInteractable -Session $Session -Filter "Media" | Format-Table

Invoke-HTMLNavigation -Session $Session -Selector 'a[href="upload\.php"]'
Save-HTMLScreenshot -OutFile ".\Output\EvotecPageAdmin5.png" -Open

Invoke-HTMLNavigation -Session $Session -Selector 'a[href="https://evotec.xyz/wp-admin/media-new.php"]' -PassThru #|
#Invoke-HTMLNavigation -Session $Session -Url 'https://evotec.xyz/wp-admin/media-new.php'
#Invoke-HTMLNavigation -Session $Session -url "media-new.php"
#Invoke-HTMLNavigation -Session $Session -Selector 'a[href="upload.php"]' -PassThru | Save-HTMLScreenshot -OutFile "$PSScriptRoot\Output\EvotecPageAdmin5.png" -Open



# Invoke-HTMLNavigation -Session $Session -Text "Profile" -PassThru | Save-HTMLScreenshot -OutFile "$PSScriptRoot\Output\EvotecPageAdmin5.png" -Open
# Close the session using new cmdlet alias (Stop-HTMLSession)
#Close-HTMLSession -Session $Session | Out-Null