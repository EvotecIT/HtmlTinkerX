Describe 'CleanHeaderName' {
    It 'Removes dashes from header name' {
        [PSParseHTML.HtmlParser]::CleanHeaderName('Header-Name') | Should -Be 'HeaderName'
    }

    It 'Cleans brackets from header name' {
        [PSParseHTML.HtmlParser]::CleanHeaderName('[Header]') | Should -Be 'Header'
    }

    It 'Replaces ampersand with and' {
        [PSParseHTML.HtmlParser]::CleanHeaderName('A&B') | Should -Be 'AandB'
    }

    It 'Removes common symbols consistently' {
        [PSParseHTML.HtmlParser]::CleanHeaderName('#Price ($)!') | Should -Be 'Price'
    }
}
