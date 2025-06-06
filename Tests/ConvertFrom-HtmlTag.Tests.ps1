Describe -Name 'ConvertFrom-HTMLTag' {
    It -Name 'Given a valid URL - Should return the content of the tag' {
        $Content = ConvertFrom-HTMLTag -Tag 'em' -Url "https://developer.mozilla.org/en-US/docs/Web/HTML/Element/em"
        $Content | Should -Contain '<em>'
        $Content = ConvertFrom-HTMLTag -Tag 'em' -Url "https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/em"
        $Content | Should -Match '<em>'
    }

    It 'ConvertFrom-HTML cmdlet works' {
        $doc = ConvertFrom-HTML -Url 'https://example.com'
        $doc | Should -Not -BeNullOrEmpty
    }
}
