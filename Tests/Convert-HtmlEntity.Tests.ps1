Describe 'ConvertFrom-HtmlEntity and ConvertTo-HtmlEntity' {
    It 'decodes HTML entities from pipeline input' {
        'A&amp;B &lt;tag&gt;' | ConvertFrom-HtmlEntity | Should -Be 'A&B <tag>'
    }

    It 'encodes text as HTML entities' {
        $encoded = ConvertTo-HtmlEntity -Text 'A&B <tag>'

        $encoded | Should -Match 'A&amp;B'
        $encoded | Should -Match '&lt;tag&gt;'
    }
}
