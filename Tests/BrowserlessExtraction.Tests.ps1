Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

Describe 'Browserless extraction pipeline' {
    It 'Exports browserless discovery, extraction, and recipe commands' {
        Get-Command Find-HtmlDataSource | Should -Not -BeNullOrEmpty
        Get-Command Invoke-HtmlDataExtraction | Should -Not -BeNullOrEmpty
        Get-Command Export-HtmlExtractionRecipe | Should -Not -BeNullOrEmpty
        Get-Command Import-HtmlExtractionRecipe | Should -Not -BeNullOrEmpty
        Get-Command Invoke-HtmlExtractionRecipe | Should -Not -BeNullOrEmpty
    }

    It 'Discovers app-state sources and extracts them without a browser' {
        $html = @'
<!doctype html>
<html>
<head>
<script id="__NEXT_DATA__" type="application/json">
{
  "props": {
    "pageProps": {
      "products": [
        { "id": "p1", "name": "Alpha" },
        { "id": "p2", "name": "Beta" }
      ]
    }
  }
}
</script>
</head>
<body><main>Loading</main></body>
</html>
'@

        $sources = Find-HtmlDataSource -Content $html -BaseUrl 'https://example.org/products' -DirectOnly
        $appState = $sources | Where-Object Kind -EQ 'AppState' | Select-Object -First 1

        $appState | Should -Not -BeNullOrEmpty
        $appState.RequiresHttpFetch | Should -BeFalse
        $result = $appState | Invoke-HtmlDataExtraction

        $result.Success | Should -BeTrue
        $result.Mode | Should -Be 'Browserless'
        $result.Items.Name | Should -Contain 'Alpha'
        $result.Items.Name | Should -Contain 'Beta'
    }

    It 'Round-trips static browserless recipes with raw content' {
        $html = @'
<!doctype html>
<html>
<head>
<script id="__NEXT_DATA__" type="application/json">
{
  "props": {
    "pageProps": {
      "products": [
        { "id": "p1", "name": "Alpha" },
        { "id": "p2", "name": "Beta" }
      ]
    }
  }
}
</script>
</head>
<body><main>Products</main></body>
</html>
'@
        $recipePath = Join-Path $TestDrive 'browserless-recipe.json'
        $source = Find-HtmlDataSource -Content $html -BaseUrl 'https://example.org/products' -DirectOnly |
            Where-Object Kind -EQ 'AppState' |
            Select-Object -First 1

        $source | Export-HtmlExtractionRecipe -Path $recipePath -IncludeRawContent -PassThru | Should -Be $recipePath
        Test-Path -LiteralPath $recipePath | Should -BeTrue

        $recipe = Import-HtmlExtractionRecipe -Path $recipePath
        $recipe.SourceKind | Should -Be 'AppState'

        $result = $recipe | Invoke-HtmlExtractionRecipe
        $result.Success | Should -BeTrue
        $result.Items.Name | Should -Contain 'Alpha'
        $result.Items.Name | Should -Contain 'Beta'
    }
}
