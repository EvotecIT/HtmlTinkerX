Import-Module "$PSScriptRoot/../PSParseHTML.psd1"

Describe 'Set-HTMLHttpClientOption' {
    It 'Updates factory timeout' {
        Set-HTMLHttpClientOption -TimeoutSeconds 5
        [HtmlTinkerX.HtmlHttpClientFactory]::DefaultTimeout.TotalSeconds | Should -Be 5
    }

    It 'Applies headers for created clients' {
        Set-HTMLHttpClientOption -Header @{ Test = 'Yes' } -ClearHeader
        $client = [HtmlTinkerX.HtmlHttpClientFactory]::Create()
        $client.DefaultRequestHeaders.GetValues('Test') | Should -Contain 'Yes'
        $client.Dispose()
    }

    It 'Validates TimeoutSeconds range' {
        { Set-HTMLHttpClientOption -TimeoutSeconds -2 } | Should -Throw
    }
}
