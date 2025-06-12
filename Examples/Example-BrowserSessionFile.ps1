Import-Module .\PSParseHTML.psd1 -Force

# When using Session, you can either save $Session variable or use the "default" session
# Default session is always used unless you specify NoSession
$null = Open-HTMLSession -Path "$PSScriptRoot\Input\Example-HierarchicalLayout01.html" -Session

Save-HTMLScreenshot -OutFile "$PSScriptRoot\Output\HierarchicalLayout01.png" -Full -Open

Get-HTMLInteractable -IncludeHidden | Format-Table

Invoke-HTMLNavigation -Selector "a#hide_anchor-y4b615h"

Save-HTMLScreenshot -OutFile "$PSScriptRoot\Output\HierarchicalLayout02.png" -Full -Open