Import-Module .\PSParseHTML.psd1 -Force

$Urls = @(
    'https://www.goal.com/en-us/champions-league/4oogyu6o156iphvdvphwpck10'
    'https://www.goal.com/en-us/champions-league/fixtures-results/4oogyu6o156iphvdvphwpck10'
    'https://www.goal.com/en-us/champions-league/table/4oogyu6o156iphvdvphwpck10'
    'https://www.goal.com/en-us/champions-league/top-players/4oogyu6o156iphvdvphwpck10'
)
foreach ($Url in $Urls) {
    $HTML = Invoke-HTMLRendering -Url $Url -Browser Chromium

    $Test = ConvertFrom-HtmlTable -Content $HTML
    for ($Count = 0; $Count -lt $Test.Count; $Count++) {
        $Test[$Count] | Format-Table -AutoSize *
    }
}