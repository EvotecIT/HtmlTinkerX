Import-Module .\PSParseHTML.psd1 -Force

<#
WARNING: Development mode: Using binaries from C:\Support\GitHub\PSParseHTML\Sources\PSParseHTML.PowerShell\bin\Debug\net8.0\PSParseHTML.PowerShell.dll
Downloading Chromium 136.0.7103.25 (playwright build v1169) from https://cdn.playwright.dev/dbazure/download/playwright/builds/chromium/1169/chromium-win64.zip
144.4 MiB [====================] 100% 0.0s
Chromium 136.0.7103.25 (playwright build v1169) downloaded to C:\Users\przemyslaw.klys\AppData\Local\ms-playwright\chromium-1169
Downloading FFMPEG playwright build v1011 from https://cdn.playwright.dev/dbazure/download/playwright/builds/ffmpeg/1011/ffmpeg-win64.zip
1.3 MiB [====================] 100% 0.0s
FFMPEG playwright build v1011 downloaded to C:\Users\przemyslaw.klys\AppData\Local\ms-playwright\ffmpeg-1011
Downloading Chromium Headless Shell 136.0.7103.25 (playwright build v1169) from https://cdn.playwright.dev/dbazure/download/playwright/builds/chromium/1169/chromium-headless-shell-win64.zip
89.1 MiB [====================] 100% 0.0s
Chromium Headless Shell 136.0.7103.25 (playwright build v1169) downloaded to C:\Users\przemyslaw.klys\AppData\Local\ms-playwright\chromium_headless_shell-1169
Downloading Winldd playwright build v1007 from https://cdn.playwright.dev/dbazure/download/playwright/builds/winldd/1007/winldd-win64.zip
0.1 MiB [====================] 100% 0.0s
Winldd playwright build v1007 downloaded to C:\Users\przemyslaw.klys\AppData\Local\ms-playwright\winldd-1007
#>

$HTML = Get-RenderedHtml -Url "https://www.evotec.xyz"
$HTML

#$HTML = Get-RenderedHtml -Url "https://www.evotec.xyz" -OutFile "$PSScriptRoot\Output\evotec.html"
#$HTML