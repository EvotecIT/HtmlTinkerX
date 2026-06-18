Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

$userDataPath = Join-Path $PSScriptRoot 'Output\real-chrome-profile'
$cdpEndpoint = 'http://127.0.0.1:9222'

# Start Chrome or Edge yourself with remote debugging and the profile you want to reuse, for example:
# chrome.exe --remote-debugging-port=9222 --user-data-dir="$userDataPath"
#
# After the browser is running, HtmlTinkerX can attach to it without owning the browser process.
$profilePath = Join-Path $PSScriptRoot 'Output\real-chrome-cdp-profile.json'
New-HtmlBrowserProfile `
    -Name 'RealChromeCdp' `
    -Path $profilePath `
    -CdpEndpointUrl $cdpEndpoint `
    -PreventSsoAutoSubmit | Out-Null

$session = Start-HtmlBrowserSession `
    -Url 'https://example.com' `
    -ProfilePath $profilePath `
    -NoDefault

try {
    Get-HtmlBrowserDiagnostics -Session $session
    Export-HtmlBrowserEvidence `
        -Session $session `
        -OutFolder (Join-Path $PSScriptRoot 'Output\real-chrome-evidence') `
        -Artifact Screenshot, Html, Text `
        -NetworkSummary
} finally {
    Close-HtmlBrowserSession -Session $session
}
