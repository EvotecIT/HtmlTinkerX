Import-Module .\PSParseHTML.psd1 -Force

# You can save the returned session object or let subsequent commands use the default session.
# The default session is used unless you pass -NoDefault when starting a separate session.
$null = Start-HtmlBrowserSession -Path "$PSScriptRoot\Input\Example-HierarchicalLayout01.html"

Save-HtmlBrowserScreenshot -OutFile "$PSScriptRoot\Output\HierarchicalLayout01.png" -Full -Open

Get-HtmlBrowserInteractable -IncludeHidden | Format-Table

Invoke-HtmlBrowserNavigation -Selector "a#hide_anchor-y4b615h"

Save-HtmlBrowserScreenshot -OutFile "$PSScriptRoot\Output\HierarchicalLayout02.png" -Full -Open
