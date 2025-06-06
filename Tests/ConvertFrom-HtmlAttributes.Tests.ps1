Describe -Name 'ConvertFrom-HTMLAttributes' {
    It -Name 'Given a valid URL - Should return the content of the tag' {
        $Content = ConvertFrom-HTMLAttributes -Tag 'em' -Url "https://developer.mozilla.org/en-US/docs/Web/HTML/Element/em"
        $Content | Should -Contain '<em>'
        $Content = ConvertFrom-HTMLAttributes -Tag 'em' -Url "https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/em"
        $Content | Should -Match '<em>'
    }
}
