Import-Module .\PSParseHTML.psd1 -Force

# Start a new HTML session and save a screenshot of the interactable elements
$Session = Start-HTMLSession -Url 'https://evotec.xyz' -Session
Save-HTMLScreenshot -Session $Session -OutFile "$PSScriptRoot\Output\Interactable1.png" -Open
# Get all interactable elements from the session and format them in a table
Get-HTMLInteractable -Session $Session | Format-Table
# Alternatively, you can use the URL directly without a session (if you don't need to maintain a session state)
Get-HTMLInteractable -Url 'https://evotec.xyz' | Format-Table
Invoke-HTMLNavigation -Session $Session -Selector "a[href='https://evotec.xyz/docs/']"
# Save a screenshot of the page after navigating to the documentation page
Save-HTMLScreenshot -Session $Session -OutFile "$PSScriptRoot\Output\Interactable2.png" -Open
# You can also get interactable elements from a local HTML file
Get-HTMLInteractable -Path "$PSScriptRoot\Input\azure_status.html" | Format-Table