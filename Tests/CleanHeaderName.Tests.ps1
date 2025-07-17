Describe 'CleanHeaderName' {
    It 'Removes dashes from header name' {
        [HtmlTinkerX.HtmlParser]::CleanHeaderName('Header-Name') | Should -Be 'HeaderName'
    }

    It 'Cleans brackets from header name' {
        [HtmlTinkerX.HtmlParser]::CleanHeaderName('[Header]') | Should -Be 'Header'
    }

    It 'Replaces ampersand with and' {
        [HtmlTinkerX.HtmlParser]::CleanHeaderName('A&B') | Should -Be 'AandB'
    }

    It 'Removes common symbols consistently' {
        [HtmlTinkerX.HtmlParser]::CleanHeaderName('#Price ($)!') | Should -Be 'Price'
    }
}
