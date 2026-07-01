param(
    [Alias('ConfigurationGateMode')]
    [ValidateSet('Manifest', 'Build', 'Publish')]
    [string] $RunMode = 'Build',

    [string] $PowerShellGalleryApiKeyPath = 'C:\Support\Important\PowerShellGalleryAPI.txt',

    [string] $GitHubApiKeyPath = 'C:\Support\Important\GitHubAPI.txt'
)

Import-Module PSPublishModule -Force -ErrorAction Stop

Build-Module -ModuleName 'PSParseHTML' -RunMode $RunMode {
    # Usual defaults as per standard module
    $Manifest = [ordered] @{
        # Minimum version of the Windows PowerShell engine required by this module
        PowerShellVersion    = '5.1'
        # Supported PSEditions
        CompatiblePSEditions = @('Desktop', 'Core')
        # ID used to uniquely identify this module
        GUID                 = 'f0387960-7034-4918-a1e1-d5847cbf90df'
        # Version number of this module.
        ModuleVersion        = '2.0.X'
        # Author of this module
        Author               = 'Przemyslaw Klys'
        # Company or vendor of this module
        CompanyName          = 'Evotec'
        # Copyright statement for this module
        Copyright            = "(c) 2011 - $((Get-Date).Year) Przemyslaw Klys @ Evotec. All rights reserved."
        # Description of the functionality provided by this module
        Description          = 'Module that allows to manipulate, parse, format and optimize HTML, JavaScript and CSS'
        # Tags applied to this module. These help with module discovery in online galleries.
        Tags                 = @('HTML', 'WWW', 'JavaScript', 'CSS', 'Windows', 'MacOS', 'Linux')
        # A URL to the main website for this project.
        ProjectUri           = 'https://github.com/EvotecIT/PSParseHTML'
        # A URL to an icon representing this module.
        IconUri              = 'https://evotec.xyz/wp-content/uploads/2018/12/PSWriteHTML.png'
        # Pre-release tag for this module.
        #PreReleaseTag        = 'Preview4'
    }
    New-ConfigurationManifest @Manifest
    # Add external module dependencies, using loop for simplicity
    New-ConfigurationModule -Type ExternalModule -Name 'Microsoft.PowerShell.Management', 'Microsoft.PowerShell.Utility'

    # Add approved modules, that can be used as a dependency, but only when specific function from those modules is used
    # And on that time only that function and dependent functions will be copied over
    # Keep in mind it has it's limits when "copying" functions such as it should not depend on DLLs or other external files
    New-ConfigurationModule -Type ApprovedModule -Name 'PSSharedGoods', 'PSWriteColor', 'Connectimo', 'PSUnifi', 'PSWebToolbox', 'PSMyPassword'

    $ConfigurationFormat = [ordered] @{
        RemoveComments                              = $false

        PlaceOpenBraceEnable                        = $true
        PlaceOpenBraceOnSameLine                    = $true
        PlaceOpenBraceNewLineAfter                  = $true
        PlaceOpenBraceIgnoreOneLineBlock            = $true

        PlaceCloseBraceEnable                       = $true
        PlaceCloseBraceNewLineAfter                 = $false
        PlaceCloseBraceIgnoreOneLineBlock           = $false
        PlaceCloseBraceNoEmptyLineBefore            = $true

        UseConsistentIndentationEnable              = $true
        UseConsistentIndentationKind                = 'space'
        UseConsistentIndentationPipelineIndentation = 'IncreaseIndentationAfterEveryPipeline'
        UseConsistentIndentationIndentationSize     = 4

        UseConsistentWhitespaceEnable               = $true
        UseConsistentWhitespaceCheckInnerBrace      = $true
        UseConsistentWhitespaceCheckOpenBrace       = $true
        UseConsistentWhitespaceCheckOpenParen       = $true
        UseConsistentWhitespaceCheckOperator        = $true
        UseConsistentWhitespaceCheckPipe            = $true
        UseConsistentWhitespaceCheckSeparator       = $true

        AlignAssignmentStatementEnable              = $true
        AlignAssignmentStatementCheckHashtable      = $true

        UseCorrectCasingEnable                      = $true
    }
    # format PSD1 and PSM1 files when merging into a single file
    # enable formatting is not required as Configuration is provided
    New-ConfigurationFormat -ApplyTo 'OnMergePSM1', 'OnMergePSD1' -Sort None @ConfigurationFormat
    # format PSD1 and PSM1 files within the module
    # enable formatting is required to make sure that formatting is applied (with default settings)
    New-ConfigurationFormat -ApplyTo 'DefaultPSD1', 'DefaultPSM1' -Sort None @ConfigurationFormat
    # when creating PSD1 use special style without comments and with only required parameters
    New-ConfigurationFormat -ApplyTo 'DefaultPSD1', 'OnMergePSD1' -PSD1Style 'Minimal'

    # configuration for documentation, at the same time it enables documentation processing
    New-ConfigurationDocumentation -Enable -PathReadme 'Docs\Readme.md' -Path 'Docs' -SyncExternalHelpToProjectRoot

    New-ConfigurationImportModule -ImportSelf #-ImportRequiredModules

    $newConfigurationBuildSplat = @{
        Enable                            = $true
        # lets sign module only on my machine for now
        SignModule                        = if ($Env:COMPUTERNAME -eq 'EVOMAGIC') { $true } else { $false }
        MergeModuleOnBuild                = $true
        MergeFunctionsFromApprovedModules = $true
        CertificateThumbprint             = '483292C9E317AA13B07BB7A96AE9D1A5ED9E7703'
        ResolveBinaryConflicts            = $true
        ResolveBinaryConflictsName        = 'PSParseHTML.PowerShell'
        NETProjectName                    = 'PSParseHTML.PowerShell'
        NETProjectPath                    = 'Sources\PSParseHTML.PowerShell\PSParseHTML.PowerShell.csproj'
        NETConfiguration                  = 'Release'
        NETFramework                      = 'net8.0', 'net472'
        NETHandleAssemblyWithSameName     = $true
        NETAssemblyLoadContext            = $true
        NETAssemblyTypeAcceleratorMode    = 'Enums'
        NETAssemblyTypeAcceleratorAssemblies = @(
            'Acornima'
            'HtmlTinkerX'
            'HtmlAgilityPack'
        )
        NETAssemblyTypeAccelerators       = @(
            'Acornima.Ast.Node'
            'Acornima.Ast.Program'
            'Acornima.Ast.Script'
            'HtmlTinkerX.HtmlBrowser'
            'HtmlTinkerX.HtmlBrowserLaunchOptions'
            'HtmlTinkerX.HtmlBrowserlessExtraction'
            'HtmlTinkerX.HtmlBrowserProfile'
            'HtmlTinkerX.HtmlBrowserRecipe'
            'HtmlTinkerX.HtmlBrowserRecipeRunOptions'
            'HtmlTinkerX.HtmlBrowserSsoField'
            'HtmlTinkerX.HtmlBrowserSsoHandoff'
            'HtmlTinkerX.HtmlCookie'
            'HtmlTinkerX.HtmlHttpClientFactory'
            'HtmlTinkerX.HtmlNetworkEntry'
            'HtmlTinkerX.HtmlOptimizer'
            'HtmlTinkerX.HtmlParser'
            'HtmlTinkerX.HtmlRenderedPageSnapshot'
            'HtmlTinkerX.HtmlResourceLink'
            'HtmlAgilityPack.HtmlDocument'
            'HtmlAgilityPack.HtmlNode'
            'HtmlAgilityPack.HtmlAttribute'
            'PSParseHTML.PowerShell.PowerShellHtmlRoute'
            'Microsoft.Playwright.IRoute'
            'Microsoft.Playwright.RouteContinueOptions'
            'Microsoft.Playwright.RouteFallbackOptions'
            'Microsoft.Playwright.RouteFulfillOptions'
        )
        DotSourceLibraries                = $true
        DotSourceClasses                  = $true
        DeleteTargetModuleBeforeBuild     = $true
        NETBinaryModuleDocumentation      = $true
        NETDevelopmentBinaries            = $true
        NETDevelopmentBinariesMode        = 'Auto'
        NETDevelopmentBinariesPath        = 'Sources\PSParseHTML.PowerShell\bin'
        NETDevelopmentSourceBootstrapperMode = 'ReplaceSingleFile'
    }

    New-ConfigurationBuild @newConfigurationBuildSplat

    New-ConfigurationProjectBuild -Name 'HtmlTinkerX' -ConfigPath 'Build\project.build.json' -Enabled:$true -BuildBeforeModule -UseAsReleaseVersionSource -ProvideLocalNuGetFeed -PublishNuget -PublishGitHub
    New-ConfigurationRelease -StageRoot 'Artefacts\UploadReady' -VersionSource ProjectBuild -PrimaryProject 'HtmlTinkerX' -BuildOrder 'Packages', 'Module' -PublishOrder 'NuGet', 'PowerShellGallery', 'GitHub'

    $newConfigurationArtefactSplat = @{
        Type                = 'Unpacked'
        Enable              = $true
        Path                = 'Artefacts\Unpacked'
        ModulesPath         = 'Modules'
        RequiredModulesPath = 'Modules'
        AddRequiredModules  = $true
    }
    New-ConfigurationArtefact @newConfigurationArtefactSplat -CopyFilesRelative
    $newConfigurationArtefactSplat = @{
        Type                = 'Packed'
        Enable              = $true
        Path                = 'Artefacts\Packed'
        ModulesPath         = 'Modules'
        RequiredModulesPath = 'Modules'
        AddRequiredModules  = $true
        ArtefactName        = 'PSParseHTML-PowerShellModule.<TagModuleVersionWithPreRelease>.zip'
        IncludeTagName      = $true
    }
    New-ConfigurationArtefact @newConfigurationArtefactSplat

    #New-ConfigurationTest -TestsPath "$PSScriptRoot\..\Tests" -Enable

    New-ConfigurationPublish -Type PowerShellGallery -FilePath $PowerShellGalleryApiKeyPath -Enabled:$false -UseAsDependencyVersionSource
    New-ConfigurationPublish -Type GitHub -FilePath $GitHubApiKeyPath -UserName 'EvotecIT' -Enabled:$false -RepositoryName 'HtmlTinkerX' -OverwriteTagName 'PSParseHTML-PowerShellModule.<TagModuleVersionWithPreRelease>'

} -ExitCode
