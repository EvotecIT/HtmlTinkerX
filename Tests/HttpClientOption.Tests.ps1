Import-Module "$PSScriptRoot/../PSParseHTML.psd1"

Describe 'Set-HTMLHttpClientOption' {
    It 'Updates factory timeout' {
        Set-HTMLHttpClientOption -TimeoutSeconds 5
        [PSParseHTML.HtmlHttpClientFactory]::DefaultTimeout.TotalSeconds | Should -Be 5
    }

    It 'Applies headers for created clients' {
        Set-HTMLHttpClientOption -Header @{ Test = 'Yes' } -ClearHeader
        $client = [PSParseHTML.HtmlHttpClientFactory]::Create()
        $client.DefaultRequestHeaders.GetValues('Test') | Should -Contain 'Yes'
        $client.Dispose()
    }

    It 'Validates TimeoutSeconds range' {
        { Set-HTMLHttpClientOption -TimeoutSeconds -2 } | Should -Throw
    }
}
