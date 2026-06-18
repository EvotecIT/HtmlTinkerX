Import-Module .\PSParseHTML.psd1 -Force

$Urls = @(
    'https://www.goal.com/en-us/champions-league/4oogyu6o156iphvdvphwpck10'
    'https://www.goal.com/en-us/champions-league/fixtures-results/4oogyu6o156iphvdvphwpck10'
    'https://www.goal.com/en-us/champions-league/table/4oogyu6o156iphvdvphwpck10'
    'https://www.goal.com/en-us/champions-league/top-players/4oogyu6o156iphvdvphwpck10'
)
foreach ($Url in $Urls) {
    $HTML = Invoke-HtmlRendering -Url $Url -Browser Chromium

    $Tables = ConvertFrom-HtmlTable -Content $HTML
    for ($Count = 0; $Count -lt $Tables.Count; $Count++) {
        $Tables[$Count] | Format-Table -AutoSize *
    }

    $List = ConvertFrom-HtmlList -Content $HTML -IncludeMetadata
    for ($Count = 0; $Count -lt $List.Count; $Count++) {
        $List[$Count].Data | Format-Table -AutoSize *
    }
}