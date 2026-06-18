Import-Module .\PSParseHTML.psd1 -Force

$Credentials = [PSCredential]::new('TestUser', (ConvertTo-SecureString -String $Env:WordpressPassword -AsPlainText -Force))
# Start a reusable browser session with the canonical session cmdlet.
$browserSessionSplat = @{
    Url     = 'https://evotec.xyz/wp-admin'
}
# You can save the returned session object or let subsequent commands use the default session.
# The default session is used unless you pass -NoDefault when starting a separate session.
$null = Start-HtmlBrowserSession @browserSessionSplat
Invoke-HtmlBrowserLogin -Username $Credentials.UserName -Password ($Credentials.GetNetworkCredential().Password)

Get-HtmlBrowserInteractable -Filter "Media" -IncludeHidden | Format-Table

Save-HtmlBrowserScreenshot -OutFile "$PSScriptRoot\Output\WP1.png" -Open

Save-HtmlBrowserPdf -OutFile "$PSScriptRoot\Output\WP1.pdf" -Open

Get-HtmlBrowserInteractable -Filter "Galleries" -IncludeHidden | Format-Table

Invoke-HtmlBrowserNavigation -Text "Galleries" -Exact

Invoke-HtmlBrowserNavigation -Selector "a[href='edit.php?post_type=thegem_gallery']"

Save-HtmlBrowserScreenshot -OutFile "$PSScriptRoot\Output\WP2.png" -Open

$HTML = Get-HtmlBrowserContent
$Tables = $HTML | ConvertFrom-HTMLTable
foreach ($Table in $Tables) {
    $Table | Format-Table -AutoSize *
}
