Import-Module .\PSParseHTML.psd1 -Force

$internetAvailable = Test-Connection -ComputerName 'www.google.com' -Count 1 -Quiet

if ($internetAvailable) {
    Save-HTMLScreenshot -Url 'https://www.goal.com/en-us/premier-league/table/2kwbbcootiqqgmrzs6o5inle5' -OutFile "$PSScriptRoot\Output\PremierLeague.png" -Full -Open
    Save-HTMLScreenshot -Url 'https://evotec.xyz' -OutFile "$PSScriptRoot\Output\EvotecPage.png" -Open -Browser Chromium
    Save-HTMLScreenshot -Url 'https://evotec.xyz' -OutFile "$PSScriptRoot\Output\EvotecPageFull.png" -Full -Open -Browser Chromium
    Save-HTMLScreenshot -Url 'https://evotec.xyz/hub' -OutFile "$PSScriptRoot\Output\EvotecPageHub.png" -Full -Open -Browser Chromium
    Save-HTMLScreenshot -Url 'https://evotec.xyz/powershell-modules/' -OutFile "$PSScriptRoot\Output\EvotecPageModules.png" -Full -Open -Browser Firefox
    Save-HTMLScreenshot -Url 'https://evotec.xyz/powershell-modules/' -OutFile "$PSScriptRoot\Output\EvotecPageModules.png" -Full -Open -Browser WebKit
    Save-HTMLScreenshot -Url 'https://evotec.xyz/powershell-modules/' -OutFile "$PSScriptRoot\Output\EvotecPageModules.png" -Open -Browser Chromium -X 100 -Y 100 -Width 500 -Height 500
} else {
    $localPath = Join-Path $PSScriptRoot 'Input\Screenshot1.html'
    Save-HTMLScreenshot -Path $localPath -OutFile "$PSScriptRoot\Output\LocalScreenshot.png" -Full -Open
}
