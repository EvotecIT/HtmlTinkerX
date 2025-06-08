Import-Module .\PSParseHTML.psd1 -Force

#$Items = Invoke-HTMLRendering -Url 'https://github.com/EvotecIT/DnsClientX/releases/tag/DnsClientX-PowerShellModule.v0.4.0'

# $Lists = ConvertFrom-HtmlList -Content $Items -AsObject
# $Lists.Count
# $Lists[0] | Format-Table
# $Lists[1] | Format-Table

Save-HTMLDownload -Url 'https://github.com/EvotecIT/DnsClientX/releases/tag/DnsClientX-PowerShellModule.v0.4.0' -Path "$PSScriptRoot\Output\Test" #-Filter 'DnsClientX-PowerShellModule.v0.4.0.zip' -Verbose






