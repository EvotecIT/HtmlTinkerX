[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $ApplicationUrl,

    [string] $LoginSuccessSelector = 'main',
    [string] $BrowserProfilePath = (Join-Path $PSScriptRoot 'Output\enterprise-login-profile.json'),
    [string] $UserDataPath = (Join-Path $PSScriptRoot 'Output\enterprise-login-user-data'),
    [string] $StatePath = (Join-Path $PSScriptRoot 'Output\enterprise-login-state.json'),
    [string] $EvidencePath = (Join-Path $PSScriptRoot 'Output\enterprise-login-evidence')
)

Import-Module "$PSScriptRoot\..\PSParseHTML.psd1" -Force

$profileDirectory = Split-Path -Parent $BrowserProfilePath
if ($profileDirectory -and -not (Test-Path -LiteralPath $profileDirectory)) {
    New-Item -ItemType Directory -Path $profileDirectory -Force | Out-Null
}

$browserProfile = New-HtmlBrowserProfile `
    -Name 'EnterpriseLogin' `
    -Scenario LoginProtected `
    -Path $BrowserProfilePath `
    -UserDataDirectory $UserDataPath `
    -BrowserChannel chromium

$session = Start-HtmlBrowserSession `
    -Url $ApplicationUrl `
    -ProfilePath $BrowserProfilePath `
    -ManualLogin `
    -LoginSuccessSelector $LoginSuccessSelector `
    -LoginTimeout 120000

try {
    Export-HtmlBrowserState -Session $session -Path $StatePath
    $evidence = Export-HtmlBrowserEvidence -Session $session -OutFolder $EvidencePath -BaseFileName login-proof -NetworkSummary

    [pscustomobject] @{
        ProfileName        = $browserProfile.Name
        BrowserProfilePath = $BrowserProfilePath
        UserDataPath       = $UserDataPath
        StatePath          = $StatePath
        EvidencePath       = $evidence.OutFolder
        ManifestPath       = $evidence.ManifestPath
        FinalUrl           = $evidence.FinalUrl
        Title              = $evidence.Title
    }
} finally {
    Close-HtmlBrowserSession -Session $session
}
