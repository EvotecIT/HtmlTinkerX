@{
    AliasesToExport        = @('Stop-HTMLSession', 'ConvertFrom-HTMLTag', 'ConvertFrom-HTMLClass', 'Format-JS', 'Start-HTMLSession', 'Open-HTMLSession', 'Save-HTMLDownload')
    Author                 = 'Przemyslaw Klys'
    CmdletsToExport        = @('Close-HTMLSession', 'ConvertFrom-HTML', 'ConvertFrom-HtmlAttributes', 'ConvertFrom-HtmlList', 'ConvertFrom-HtmlTable', 'Convert-HTMLToText', 'Format-CSS', 'Format-HTML', 'Format-JavaScript', 'Get-HTMLContent', 'Get-HTMLCookie', 'Get-HTMLInteractable', 'Invoke-HTMLClick', 'Invoke-HTMLNavigation', 'Invoke-HTMLRendering', 'Invoke-HTMLScript', 'Optimize-CSS', 'Optimize-Email', 'Optimize-HTML', 'Optimize-JavaScript', 'Save-HTMLAttachment', 'Save-HTMLPdf', 'Save-HTMLScreenshot', 'Set-HTMLChecked', 'Set-HTMLCookie', 'Set-HTMLInput', 'Set-HTMLSelectOption', 'Start-HTMLVideoRecording', 'Stop-HTMLVideoRecording')
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
            Prerelease                 = 'Preview2'
            ProjectUri                 = 'https://github.com/EvotecIT/PSParseHTML'
            Tags                       = @('HTML', 'WWW', 'JavaScript', 'CSS', 'Windows', 'MacOS', 'Linux')
        }
    }
    RequiredModules        = @('Microsoft.PowerShell.Management', 'Microsoft.PowerShell.Utility')
    RootModule             = 'PSParseHTML.psm1'
}