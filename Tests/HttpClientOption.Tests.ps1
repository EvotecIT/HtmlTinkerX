Import-Module "$PSScriptRoot/../PSParseHTML.psd1"

Describe 'Set-HtmlBrowserClientOption' {
    It 'Updates factory timeout' {
        Set-HtmlBrowserClientOption -TimeoutSeconds 5
        [HtmlTinkerX.HtmlHttpClientFactory]::DefaultTimeout.TotalSeconds | Should -Be 5
    }

    It 'Applies headers for created clients' {
        Set-HtmlBrowserClientOption -Header @{ Test = 'Yes' } -ClearHeader
        $client = [HtmlTinkerX.HtmlHttpClientFactory]::Create()
        $client.DefaultRequestHeaders.GetValues('Test') | Should -Contain 'Yes'
        ($client.DefaultRequestHeaders.GetValues('User-Agent') -join ' ') | Should -Match '^HtmlTinkerX/'
        $client.Dispose()
    }

    It 'Validates TimeoutSeconds range' {
        { Set-HtmlBrowserClientOption -TimeoutSeconds -2 } | Should -Throw
    }
}
