Import-Module "$PSScriptRoot/../PSParseHTML.psd1"

Describe 'Set-HtmlBrowserClientOption' {
    BeforeEach {
        $originalTimeout = [HtmlTinkerX.HtmlHttpClientFactory]::DefaultTimeout
        $originalHeaders = @{}
        foreach ($header in [HtmlTinkerX.HtmlHttpClientFactory]::DefaultHeaders.GetEnumerator()) {
            $originalHeaders[$header.Key] = $header.Value
        }
    }

    AfterEach {
        [HtmlTinkerX.HtmlHttpClientFactory]::DefaultTimeout = $originalTimeout
        [HtmlTinkerX.HtmlHttpClientFactory]::DefaultHeaders.Clear()
        foreach ($header in $originalHeaders.GetEnumerator()) {
            [HtmlTinkerX.HtmlHttpClientFactory]::DefaultHeaders[$header.Key] = $header.Value
        }
        [HtmlTinkerX.HtmlHttpClientFactory]::ResetShared()
    }

    It 'Updates factory timeout' {
        Set-HtmlBrowserClientOption -TimeoutSeconds 5
        [HtmlTinkerX.HtmlHttpClientFactory]::DefaultTimeout.TotalSeconds | Should -Be 5
    }

    It 'Applies headers for created clients' {
        Set-HtmlBrowserClientOption -Header @{ Test = 'Yes' } -ClearHeader
        $client = [HtmlTinkerX.HtmlHttpClientFactory]::Create()
        try {
            $client.DefaultRequestHeaders.GetValues('Test') | Should -Contain 'Yes'
            ($client.DefaultRequestHeaders.GetValues('User-Agent') -join ' ') | Should -Match '^HtmlTinkerX/'
        } finally {
            $client.Dispose()
        }
    }

    It 'Validates TimeoutSeconds range' {
        { Set-HtmlBrowserClientOption -TimeoutSeconds -2 } | Should -Throw
    }
}
