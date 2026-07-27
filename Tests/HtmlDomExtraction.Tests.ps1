Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force
. "$PSScriptRoot/Support/HtmlRedirectTestServer.ps1"

Describe 'Static HTML selector discovery and object extraction' {
    BeforeAll {
        $script:Html = @'
<!doctype html>
<html>
<head><base href="https://shop.example/products/"></head>
<body>
  <article class="product product-card">
    <a class="product-link" href="one"><span class="product-title">Gold One</span></a>
    <span class="product-price" data-type="sell">10 &amp; 50 PLN</span>
  </article>
  <article class="product product-card">
    <a class="product-link" href="two"><span class="product-title">Gold Two</span></a>
    <span class="product-price" data-type="sell">20 PLN</span>
  </article>
</body>
</html>
'@
    }

    It 'exports static selector commands and the intuitive Find alias' {
        Get-Command Select-HtmlElement | Should -Not -BeNullOrEmpty
        Get-Command Find-HtmlSelector | Should -Not -BeNullOrEmpty
        (Get-Alias Find-HtmlElement).Definition | Should -Be 'Select-HtmlElement'
    }

    It 'selects static elements with CSS and composes existing value cmdlets' {
        $title = Select-HtmlElement -Content $script:Html -Selector '.product-title' -First
        $link = Select-HtmlElement -Content $script:Html -Selector '.product-link' -First

        ($title | Select-HtmlInnerText) | Should -Be 'Gold One'
        ($link | Select-HtmlAttributeValue -AttributeName href) | Should -Be 'one'
    }

    It 'converts repeated items into ordinary PowerShell objects' {
        $items = Select-HtmlData -Content $script:Html -BaseUrl 'https://shop.example/catalog' `
            -ItemSelector '.product-card' `
            -Property @{
                Name = '.product-title'
                Price = ".product-price[data-type='sell']"
                Link = @{ Selector = '.product-link'; Attribute = 'href' }
            }

        $items | Should -HaveCount 2
        $items[0].Name | Should -Be 'Gold One'
        $items[0].Price | Should -Be '10 & 50 PLN'
        $items[0].Link | Should -Be 'https://shop.example/products/one'
    }

    It 'discovers repeated items, fields, links, and a ready extraction command' {
        $candidate = Find-HtmlSelector -Content $script:Html -BaseUrl 'https://shop.example/catalog' -Query Gold |
            Where-Object Selector -Match 'product-card' |
            Select-Object -First 1

        $candidate.MatchCount | Should -Be 2
        $candidate.Fields.Name | Should -Contain 'Title'
        $candidate.Fields.Name | Should -Contain 'ProductLink'
        $candidate.Fields.Name | Should -Contain 'SellPrice'
        $candidate.SuggestedCommand | Should -Match 'Select-HtmlData'
        $candidate.SuggestedCommand | Should -Match 'ItemSelector'
    }

    It 'exposes request-specific header overrides on direct URL commands' {
        (Get-Command ConvertFrom-Html).Parameters.Keys | Should -Contain 'UserAgent'
        (Get-Command ConvertFrom-Html).Parameters.Keys | Should -Contain 'Header'
        (Get-Command Select-HtmlData).Parameters.Keys | Should -Contain 'UserAgent'
        (Get-Command Find-HtmlSelector).Parameters.Keys | Should -Contain 'Header'
    }

    It 'uses the final redirect URL for generated commands and relative links' {
        $server = [HtmlRedirectTestServer]::new()
        try {
            $startUrl = $server.Url + 'redirect-selector'
            $candidate = Find-HtmlSelector -Url $startUrl -Query 'one' -Limit 1
            $items = Invoke-Expression $candidate.SuggestedCommand

            $candidate.Selector | Should -Be 'article.product-card'
            $candidate.SuggestedCommand | Should -Match 'redirect-selector'
            $candidate.SuggestedCommandIsReplayable | Should -BeTrue
            $items | Should -HaveCount 2
            $items[0].ProductLink | Should -Be ($server.Url + 'final/catalog/one')

            $direct = Select-HtmlData -Url $startUrl -ItemSelector 'article.product-card' -Property @{
                Link = @{ Selector = 'a'; Attribute = 'href' }
            }
            $direct[1].Link | Should -Be ($server.Url + 'final/catalog/two')
        } finally {
            $server.Dispose()
        }
    }
}
