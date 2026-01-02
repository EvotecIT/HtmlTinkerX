@{
    AliasesToExport        = @('Close-HTMLSession', 'Stop-HTMLSession', 'ConvertFrom-HTMLTag', 'ConvertFrom-HTMLClass', 'Export-HTMLSession', 'Format-JS', 'Get-HTMLConsoleLog', 'Get-HTMLContent', 'Get-HTMLCookie', 'Get-HTMLFormField', 'Get-HTMLInteractable', 'Get-HTMLLoginForm', 'Get-HTMLNetworkLog', 'Import-HTMLSession', 'Invoke-HTMLClick', 'Invoke-HTMLDomScript', 'Invoke-HTMLLogin', 'Invoke-HTMLNavigation', 'Invoke-HTMLScript', 'Start-HTMLSession', 'Open-HTMLSession', 'Measure-HTMLPerformance', 'Measure-HTMLDocument', 'New-HTMLCookie', 'Save-HTMLAttachment', 'Save-HTMLDownload', 'Save-HTMLHar', 'Save-HTMLPdf', 'Save-HTMLScreenshot', 'Set-HTMLChecked', 'Set-HTMLHttpClientOption', 'Set-HTMLCookie', 'Set-HTMLInput', 'Set-HTMLSelectOption', 'Show-HTMLHar', 'Start-HTMLTracing', 'Start-HTMLVideoRecording', 'Stop-HTMLTracing', 'Stop-HTMLVideoRecording', 'Submit-HTMLForm')
    Author                 = 'Przemyslaw Klys'
    CmdletsToExport        = @('Close-HtmlBrowserSession', 'Compare-HTML', 'ConvertFrom-HTML', 'ConvertFrom-HtmlAttributes', 'ConvertFrom-HTMLCookie', 'ConvertFrom-HtmlForm', 'ConvertFrom-HtmlList', 'ConvertFrom-HtmlMeta', 'ConvertFrom-HtmlMicrodata', 'ConvertFrom-HtmlOpenGraph', 'ConvertFrom-HtmlTable', 'Convert-HTMLToText', 'Export-BrowserState', 'Export-HtmlBrowserSession', 'Export-HTMLOutline', 'Format-CSS', 'Format-HTML', 'Format-JavaScript', 'Get-HtmlBrowserConsoleLog', 'Get-HtmlBrowserContent', 'Get-HtmlBrowserCookie', 'Get-HtmlBrowserFormField', 'Get-HtmlBrowserInteractable', 'Get-HtmlBrowserLoginForm', 'Get-HtmlBrowserNetworkLog', 'Get-HTMLResource', 'Import-BrowserState', 'Import-HtmlBrowserSession', 'Invoke-HtmlBrowserClick', 'Invoke-HtmlBrowserDomScript', 'Invoke-HtmlBrowserLogin', 'Invoke-HtmlBrowserNavigation', 'Invoke-HtmlBrowserScript', 'Invoke-HTMLRendering', 'Measure-HtmlBrowserPerformance', 'Measure-HtmlDocumentStructure', 'New-HtmlBrowserCookie', 'Optimize-CSS', 'Optimize-Email', 'Optimize-HTML', 'Optimize-JavaScript', 'Register-HTMLRoute', 'Save-HtmlBrowserAttachment', 'Save-HtmlBrowserHar', 'Save-HtmlBrowserPdf', 'Save-HtmlBrowserScreenshot', 'Set-HtmlBrowserChecked', 'Set-HtmlBrowserClientOption', 'Set-HtmlBrowserCookie', 'Set-HtmlBrowserInput', 'Set-HtmlBrowserSelectOption', 'Show-HtmlBrowserHar', 'Start-HtmlBrowserTracing', 'Start-HtmlBrowserVideoCapture', 'Stop-HtmlBrowserTracing', 'Stop-HtmlBrowserVideoCapture', 'Submit-HtmlBrowserForm', 'Test-HtmlMicrodata', 'Unregister-HTMLRoute', 'Test-HtmlBrowser', 'Clear-HtmlBrowserCache')
    CompanyName            = 'Evotec'
    CompatiblePSEditions   = @('Desktop', 'Core')
    Copyright              = '(c) 2011 - 2026 Przemyslaw Klys @ Evotec. All rights reserved.'
    Description            = 'Module that allows to manipulate, parse, format and optimize HTML, JavaScript and CSS'
    DotNetFrameworkVersion = '4.7.2'
    FunctionsToExport      = @()
    GUID                   = 'f0387960-7034-4918-a1e1-d5847cbf90df'
    ModuleVersion          = '2.0.5'
    PowerShellVersion      = '5.1'
    PrivateData            = @{
        PSData = @{
            ExternalModuleDependencies = @('Microsoft.PowerShell.Management', 'Microsoft.PowerShell.Utility')
            IconUri                    = 'https://evotec.xyz/wp-content/uploads/2018/12/PSWriteHTML.png'
            ProjectUri                 = 'https://github.com/EvotecIT/PSParseHTML'
            RequireLicenseAcceptance   = $false
            Tags                       = @('HTML', 'WWW', 'JavaScript', 'CSS', 'Windows', 'MacOS', 'Linux')
        }
    }
    RequiredModules        = @('Microsoft.PowerShell.Management', 'Microsoft.PowerShell.Utility')
    RootModule             = 'PSParseHTML.psm1'
}