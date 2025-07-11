@{
    AliasesToExport        = @('Stop-HTMLSession', 'ConvertFrom-HTMLTag', 'ConvertFrom-HTMLClass', 'Format-JS', 'Start-HTMLSession', 'Open-HTMLSession', 'Save-HTMLDownload')
    Author                 = 'Przemyslaw Klys'
    CmdletsToExport        = @('Close-HTMLSession', 'Compare-HTML', 'ConvertFrom-HTML', 'ConvertFrom-HtmlAttributes', 'ConvertFrom-HtmlForm', 'ConvertFrom-HtmlList', 'ConvertFrom-HtmlMeta', 'ConvertFrom-HtmlMicrodata', 'ConvertFrom-HtmlOpenGraph', 'ConvertFrom-HtmlTable', 'Convert-HTMLToText', 'Export-HTMLSession', 'Format-CSS', 'Format-HTML', 'Format-JavaScript', 'Get-HTMLConsoleLog', 'Get-HTMLContent', 'Get-HTMLCookie', 'Get-HTMLInteractable', 'Get-HTMLLoginForm', 'Get-HTMLNetworkLog', 'Import-HTMLSession', 'Invoke-HTMLClick', 'Invoke-HTMLDomScript', 'Invoke-HTMLNavigation', 'Invoke-HTMLRendering', 'Invoke-HTMLScript', 'Optimize-CSS', 'Optimize-Email', 'Optimize-HTML', 'Optimize-JavaScript', 'Register-HTMLRoute', 'Save-HTMLAttachment', 'Save-HTMLHar', 'Save-HTMLPdf', 'Save-HTMLScreenshot', 'Set-HTMLChecked', 'Set-HTMLCookie', 'Set-HTMLHttpClientOption', 'Set-HTMLInput', 'Set-HTMLSelectOption', 'Show-HTMLHar', 'Start-HTMLTracing', 'Start-HTMLVideoRecording', 'Stop-HTMLTracing', 'Stop-HTMLVideoRecording', 'Submit-HTMLForm', 'Unregister-HTMLRoute', 'Measure-HTMLDocument', 'Get-HTMLResource')
    CompanyName            = 'Evotec'
    CompatiblePSEditions   = @('Desktop', 'Core')
    Copyright              = '(c) 2011 - 2025 Przemyslaw Klys @ Evotec. All rights reserved.'
    Description            = 'Module that allows to manipulate, parse, format and optimize HTML, JavaScript and CSS'
    DotNetFrameworkVersion = '4.7.2'
    FunctionsToExport      = @()
    GUID                   = 'f0387960-7034-4918-a1e1-d5847cbf90df'
    ModuleVersion          = '2.0.0'
    PowerShellVersion      = '5.1'
    PrivateData            = @{
        PSData = @{
            ExternalModuleDependencies = @('Microsoft.PowerShell.Management', 'Microsoft.PowerShell.Utility')
            IconUri                    = 'https://evotec.xyz/wp-content/uploads/2018/12/PSWriteHTML.png'
            Prerelease                 = 'Preview3'
            ProjectUri                 = 'https://github.com/EvotecIT/PSParseHTML'
            Tags                       = @('HTML', 'WWW', 'JavaScript', 'CSS', 'Windows', 'MacOS', 'Linux')
        }
    }
    RequiredModules        = @('Microsoft.PowerShell.Management', 'Microsoft.PowerShell.Utility')
    RootModule             = 'PSParseHTML.psm1'
}