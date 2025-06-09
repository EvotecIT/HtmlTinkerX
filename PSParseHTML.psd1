@{
    AliasesToExport        = @('Stop-HTMLSession', 'ConvertFrom-HTMLTag', 'ConvertFrom-HTMLClass', 'Format-JS', 'Start-HTMLSession', 'Open-HTMLSession', 'Save-HTMLAttachment')
    Author                 = 'Przemyslaw Klys'
    CmdletsToExport        = @('Close-HTMLSession', 'ConvertFrom-HTML', 'ConvertFrom-HtmlAttributes', 'ConvertFrom-HtmlList', 'ConvertFrom-HtmlTable', 'Convert-HTMLToText', 'Format-CSS', 'Format-HTML', 'Format-JavaScript', 'Get-HTMLInteractable', 'Invoke-HTMLNavigation', 'Invoke-HTMLRendering', 'Optimize-CSS', 'Optimize-Email', 'Optimize-HTML', 'Optimize-JavaScript', 'Save-HTMLDownload', 'Save-HTMLScreenshot')
    CompanyName            = 'Evotec'
    CompatiblePSEditions   = @('Desktop', 'Core')
    Copyright              = '(c) Przemyslaw Klys. All rights reserved.'
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
            ProjectUri                 = 'https://github.com/EvotecIT/PSParseHTML'
            Tags                       = @('HTML', 'WWW', 'JavaScript', 'CSS', 'Windows', 'MacOS', 'Linux')
        }
    }
    RequiredModules        = @('Microsoft.PowerShell.Management', 'Microsoft.PowerShell.Utility')
    RootModule             = 'PSParseHTML.psm1'
}