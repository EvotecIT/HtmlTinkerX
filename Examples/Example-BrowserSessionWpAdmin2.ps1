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
$Session = Start-HtmlBrowserSession @browserSessionSplat

Get-HtmlBrowserInteractable -Session $Session -Filter "Media" -IncludeHidden | Format-Table

<# Output example, this is what you can expect from Get-HtmlBrowserInteractable cmdlet, Visible is True if the element is visible on the page, Selector is a CSS selector that can be used to interact with the element, Href is the link associated with the element, Tag is the HTML tag of the element, Id is the ID of the element if it exists, and Class is the class of the element if it exists.
Index Text           Visible Selector                                                   Href                                      Tag Id Class
----- ----           ------- --------                                                   ----                                      --- -- -----
   10 Media             True a[href="upload\.php"]                                      upload.php                                a      wp-has-submenu wp-not-current-submenu menu-top menu-icon-media
   12 Add Media File    True a[href="media-new\.php"]                                   media-new.php                             a
   51 Media            False a[href="https\:\/\/evotec\.xyz\/wp-admin\/media-new\.php"] https://evotec.xyz/wp-admin/media-new.php a      ab-item
#>

# This will fail because there are multiple elements with the same text
Invoke-HtmlBrowserNavigation -Session $Session -Text "Media"
# This will return all interactable elements on the page that match the filter "Media", but will include hidden elements
Get-HtmlBrowserInteractable -Session $Session -Filter "Media" | Format-Table
# This will navigate to the Media page using the selector
Invoke-HtmlBrowserNavigation -Session $Session -Selector 'a[href="upload.php"]'
# This will save a screenshot of the page after navigating to the upload.php page
Save-HtmlBrowserScreenshot -OutFile ".\Output\EvotecPageAdmin5.png" -Open
# This will navigate to the Media page using the selector
Invoke-HtmlBrowserNavigation -Session $Session -Selector 'a[href="https://evotec.xyz/wp-admin/media-new.php"]'
# This will navigate to the Media page using Url
Invoke-HtmlBrowserNavigation -Session $Session -Url 'https://evotec.xyz/wp-admin/media-new.php'
# This will return all interactable elements on the page that match the filter "Media"
Get-HtmlBrowserInteractable -Session $Session -Filter "Media" | Format-Table
# This will save a screenshot of the page after navigating to the upload.php page
Invoke-HtmlBrowserNavigation -Session $Session -Selector 'a[href="upload.php"]' -PassThru | Save-HtmlBrowserScreenshot -OutFile "$PSScriptRoot\Output\EvotecPageAdmin5.png" -Open
# Close the session
Close-HtmlBrowserSession -Session $Session | Out-Null
