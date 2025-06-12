Import-Module .\PSParseHTML.psd1 -Force

New-HTML -Online {
    New-HTMLTable -DataTable 'test'
} -FilePath $PSScriptRoot\Input\Test.html #-ShowHTML

Format-HTML -File $PSScriptRoot\Input\Test.html -OutputFile $PSScriptRoot\Output\Test1.html
Optimize-HTML -File $PSScriptRoot\Input\Test.html -OutputFile $PSScriptRoot\Output\Test2.html