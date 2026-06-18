Import-Module ..\PSParseHTML.psd1 -Force

# Stream downloads as they complete
foreach ($file in Save-HtmlBrowserAttachment -Url 'https://github.com/EvotecIT/DnsClientX/releases/tag/DnsClientX-PowerShellModule.v0.4.0' -Path "$PSScriptRoot\Output\Streaming" -Filter '.zip') {
    Write-Host "Downloaded $file"
}
