Import-Module .\PSParseHTML.psd1 -Force

$BaseUri = 'https://www.acunetix.com/vulnerabilities/web/page/'

[Array] $Vulnerabilities = foreach ($page in (1..2)) {
    $ProgressPreference = 'SilentlyContinue'
    $html = (Invoke-WebRequest -Uri "$BaseUri/$page/").Content
    #$AgilityPackTable = ConvertFrom-HtmlTable -Content $html -Engine AgilityPack -ReplaceHeaders $ReplaceHeaders
    $AngleSharpTable = ConvertFrom-HtmlTable -Content $html -Engine AngleSharp -ReplaceHeaders $ReplaceHeaders

    foreach ($row in $AngleSharpTable) {
        $Vulnerability = [PSCustomObject]@{
            'Vulnerability Name' = $row.'Vulnerability Name'
            CVE                  = $row."CVE`n`nCWE"
            CWE                  = $row.CWE
            Severity             = $row.Severity
        }
        $Vulnerability
    }
}

$Vulnerabilities | Format-Table