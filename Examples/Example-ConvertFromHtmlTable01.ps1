
Import-Module .\PSParseHTML.psd1 -Force


$Tables = ConvertFrom-HtmlTable -Url "https://www.semperis.com/purple-knight/security-indicators/" -IncludeMetadata
$Tables

return

$html = (Invoke-WebRequest -Uri 'https://en.wikipedia.org/wiki/PowerShell').Content
$Tables = ConvertFrom-HtmlTable -Content $html
foreach ($Table in $Tables) {
    $Table | Format-Table -AutoSize *
}

# Alternatively
$Tables = ConvertFrom-HtmlTable -Url 'https://en.wikipedia.org/wiki/PowerShell'
# If converting multiple tables, the output will look funky
# since it is creating an array of different objects.
$Tables[0] | Format-Table -AutoSize
$Tables[1] | Format-Table -AutoSize
$Tables[2] | Format-Table -AutoSize
$Tables[3] | Format-Table -AutoSize
# ... etc