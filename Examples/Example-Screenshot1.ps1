Import-Module .\PSParseHTML.psd1 -Force

Save-HTMLScreenshot -Url 'https://www.goal.com/en-us/premier-league/table/2kwbbcootiqqgmrzs6o5inle5' -OutFile "$PSScriptRoot\Output\PremierLeague.png"

Save-HTMLScreenshot -Url 'https://evotec.xyz' -OutFile "$PSScriptRoot\Output\EvotecPage.png"