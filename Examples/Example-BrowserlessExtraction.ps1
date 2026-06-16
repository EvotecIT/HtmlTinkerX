Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

$html = @'
<!doctype html>
<html>
<head>
<title>Browserless extraction story</title>
<script id="__NEXT_DATA__" type="application/json">
{
  "props": {
    "pageProps": {
      "products": [
        { "id": "p1", "name": "HtmlTinkerX guide", "category": "Docs" },
        { "id": "p2", "name": "Browserless recipe", "category": "Automation" }
      ]
    }
  }
}
</script>
<script>
fetch('/api/products', { method: 'GET' });
</script>
</head>
<body>
<main>Products are hydrated from embedded state.</main>
</body>
</html>
'@

$recipePath = Join-Path ([System.IO.Path]::GetTempPath()) 'psparsehtml-browserless-recipe.json'
$sources = Find-HtmlDataSource -Content $html -BaseUrl 'https://example.org/products' -DirectOnly
$source = $sources | Where-Object Kind -EQ 'AppState' | Select-Object -First 1
$result = $source | Invoke-HtmlDataExtraction
$source | Export-HtmlExtractionRecipe -Path $recipePath -IncludeRawContent -PassThru | Out-Null
$recipeResult = Import-HtmlExtractionRecipe -Path $recipePath | Invoke-HtmlExtractionRecipe

[pscustomobject]@{
    Mode = $result.Mode
    SourceKind = $source.Kind
    SourceName = $source.Name
    ProductNames = $result.Items.Name
    RecipePath = $recipePath
    RecipeItemCount = $recipeResult.Items.Count
    BrowserStarted = $false
}
