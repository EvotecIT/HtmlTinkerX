Import-Module PSPublishModule

Get-ProjectVersion -Path "C:\Support\GitHub\PSParseHTML" -ExcludeFolders "C:\Support\GitHub\PSParseHTML\Module\Artefacts"
Set-ProjectVersion -Path "C:\Support\GitHub\PSParseHTML" -NewVersion "2.0.1"