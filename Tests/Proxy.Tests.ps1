Describe 'Proxy parameters' {
    It 'Cmdlets expose proxy parameters' {
        $cmdlets = 'ConvertFrom-HTML','ConvertFrom-HtmlTable','ConvertFrom-HtmlAttributes','ConvertFrom-HtmlList','Convert-HTMLToText','Invoke-HTMLRendering','Save-HTMLScreenshot','Save-HTMLPdf','Save-HTMLAttachment','Get-HTMLInteractable','Start-HTMLVideoRecording'
        foreach($cmd in $cmdlets){
            $params = (Get-Command $cmd).Parameters.Keys
            $params | Should -Contain 'Proxy'
            $params | Should -Contain 'ProxyCredential'
        }
    }
}

