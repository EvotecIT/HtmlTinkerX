Import-Module .\PSParseHTML.psd1 -Force

$HTML = Invoke-HtmlRendering -Url "https://portal.assessor.lacounty.gov/parceldetail/5130020021" -Browser Chromium

$PageHTMLUrl2 = ConvertFrom-HTMLTag -Content $HTML -Class 'MuiTypography-root'
$PageHTMLUrl2 | Format-Table

$Tables = ConvertFrom-HtmlTable -Content $HTML
for ($Count = 0; $Count -lt $Tables.Count; $Count++) {
    $Tables[$Count] | Format-Table -AutoSize *
}