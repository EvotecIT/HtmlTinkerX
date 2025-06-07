@{
    AliasesToExport        = @('ConvertFrom-HTMLTag', 'ConvertFrom-HTMLClass', 'Format-JS')
    Author                 = 'Przemyslaw Klys'
    CmdletsToExport        = @('ConvertFrom-HTML', 'ConvertFrom-HtmlAttributes', 'ConvertFrom-HtmlTable', 'Convert-HTMLToText', 'Format-CSS', 'Format-HTML', 'Format-JavaScript', 'Optimize-CSS', 'Optimize-Email', 'Optimize-HTML', 'Optimize-JavaScript')
    CompanyName            = 'Evotec'
    CompatiblePSEditions   = @('Desktop', 'Core')
    Copyright              = '(c) Przemyslaw Klys. All rights reserved.'
    Description            = 'Module that allows to manipulate, parse, format and optimize HTML, JavaScript and CSS'
    DotNetFrameworkVersion = '4.7.2'
    FunctionsToExport      = @('ConvertFrom-HtmlTableOld')
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