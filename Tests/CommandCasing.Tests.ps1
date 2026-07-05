Import-Module "$PSScriptRoot/../PSParseHTML.psd1" -Force

Describe 'Html and Css command casing' {
    It 'exports preferred Html and Css casing for canonical commands' {
        (Get-Command ConvertFrom-Html).Name | Should -Be 'ConvertFrom-Html'
        (Get-Command Convert-HtmlToMarkdown).Name | Should -Be 'Convert-HtmlToMarkdown'
        (Get-Command Invoke-HtmlRendering).Name | Should -Be 'Invoke-HtmlRendering'
        (Get-Command Invoke-HtmlCrawl).Name | Should -Be 'Invoke-HtmlCrawl'
        (Get-Command Format-Css).Name | Should -Be 'Format-Css'
        (Get-Command Optimize-Html).Name | Should -Be 'Optimize-Html'
        (Get-Command Complete-HtmlRoute).Name | Should -Be 'Complete-HtmlRoute'
        (Get-Command Register-HtmlRoute).Name | Should -Be 'Register-HtmlRoute'
    }

    It 'keeps old uppercase spellings resolvable through PowerShell case-insensitive lookup' {
        (Get-Command ConvertFrom-HTML).Name | Should -Be 'ConvertFrom-Html'
        (Get-Command Invoke-HTMLRendering).Name | Should -Be 'Invoke-HtmlRendering'
        (Get-Command Invoke-HTMLCrawl).Name | Should -Be 'Invoke-HtmlCrawl'
        (Get-Command Format-CSS).Name | Should -Be 'Format-Css'
        (Get-Command Complete-HTMLRoute).Name | Should -Be 'Complete-HtmlRoute'
        (Get-Command Register-HTMLRoute).Name | Should -Be 'Register-HtmlRoute'
    }

    It 'exports preferred Html casing for compatibility aliases without duplicate uppercase aliases' {
        (Get-Alias ConvertFrom-HtmlClass).Name | Should -Be 'ConvertFrom-HtmlClass'
        (Get-Alias Start-HtmlSession).Name | Should -Be 'Start-HtmlSession'
        (Get-Alias Save-HtmlScreenshot).Name | Should -Be 'Save-HtmlScreenshot'
        (Get-Alias Submit-HtmlForm).Name | Should -Be 'Submit-HtmlForm'

        (Get-Alias ConvertFrom-HTMLClass).Name | Should -Be 'ConvertFrom-HtmlClass'
        (Get-Alias Start-HTMLSession).Name | Should -Be 'Start-HtmlSession'
        (Get-Alias Save-HTMLScreenshot).Name | Should -Be 'Save-HtmlScreenshot'
        (Get-Alias Submit-HTMLForm).Name | Should -Be 'Submit-HtmlForm'
    }
}
