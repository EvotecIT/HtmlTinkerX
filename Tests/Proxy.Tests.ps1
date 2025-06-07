Describe 'Proxy parameters' {
    It 'Cmdlets expose proxy parameters' {
        $cmdlets = 'ConvertFrom-HTML','ConvertFrom-HtmlTable','ConvertFrom-HtmlAttributes','Convert-HTMLToText'
        foreach($cmd in $cmdlets){
            $params = (Get-Command $cmd).Parameters.Keys
            $params | Should -Contain 'Proxy'
            $params | Should -Contain 'ProxyCredential'
        }
    }
}

