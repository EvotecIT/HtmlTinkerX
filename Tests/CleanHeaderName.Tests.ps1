Describe 'CleanHeaderName' {
    It 'Converts dashes to underscores' {
        [PSParseHTML.HtmlParser]::CleanHeaderName('Header-Name') | Should -Be 'Header_Name'
    }

    It 'Cleans brackets from header name' {
        [PSParseHTML.HtmlParser]::CleanHeaderName('[Header]') | Should -Be 'Header'
    }

    It 'Removes ampersand' {
        [PSParseHTML.HtmlParser]::CleanHeaderName('A&B') | Should -Be 'AB'
    }

    It 'Removes common symbols consistently' {
        [PSParseHTML.HtmlParser]::CleanHeaderName('#Price ($)!') | Should -Be 'Price'
    }
}
