Describe -Name 'ConvertFrom-HTMLTag' {
    It -Name 'Given a valid URL - Should return the content of the tag' {
        $Content = ConvertFrom-HTMLTag -Tag 'em' -Url "https://developer.mozilla.org/en-US/docs/Web/HTML/Element/em"
        $Content | Should -Contain '<em>'
    }
}