Import-Module .\PSParseHTML.psd1 -Force

# Download all files
Save-HTMLDownload -Url 'https://github.com/EvotecIT/DnsClientX/releases/tag/DnsClientX-PowerShellModule.v0.4.0' -Path "$PSScriptRoot\Output\Test" -Verbose
# Download specific file
Save-HTMLDownload -Url 'https://github.com/EvotecIT/DnsClientX/releases/tag/DnsClientX-PowerShellModule.v0.4.0' -Path "$PSScriptRoot\Output\Test" -Filter 'DnsClientX-PowerShellModule.v0.4.0.zip'
# Download all zip files
Save-HTMLDownload -Url 'https://github.com/EvotecIT/DnsClientX/releases/tag/DnsClientX-PowerShellModule.v0.4.0' -Path "$PSScriptRoot\Output\Test" -Filter ".zip"
# Download all tar.gz files
Save-HTMLDownload -Url 'https://github.com/EvotecIT/DnsClientX/releases/tag/DnsClientX-PowerShellModule.v0.4.0' -Path "$PSScriptRoot\Output\Test" -Filter ".tar.gz"