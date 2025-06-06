function ConvertFrom-HTML {
    <#
    .SYNOPSIS
    Converts HTML to PowerShell object that can be further digested in PowerShell

    .DESCRIPTION
    Converts HTML to PowerShell object that can be further digested in PowerShell.
    To be used if ConvertTo-HTMLAttributes or ConvertTo-HTMLTable are not enough.

    .PARAMETER Content
    Provide HTML content to be converted to PowerShell object.

    .PARAMETER Url
    Provide URL to be converted to PowerShell object.

    .PARAMETER Engine
    Define engin to be used for conversion. Options are AgilityPack and AngleSharp.
    Both do similar stuff, but slightly in different way giving different PowerShell objects.
    Default is AgilityPack.

    .PARAMETER Raw
    Tells the function to return DocumentNode/DocumentElement instead of root object, which holds more information, that may not be nessecary for day to day use.

    .EXAMPLE
    # Option 1 - uses Agility Pack
    $PageHTML = ConvertFrom-HTML -URL "https://www.evotec.xyz"
    $PageHTML

    .EXAMPLE
    # Option 2 - uses AngleSharp
    $PageHTML = ConvertFrom-HTML -URL "https://www.evotec.xyz" -Engine AngleSharp
    $PageHTML

    .NOTES
    General notes
    #>
    [cmdletbinding(DefaultParameterSetName = 'Uri')]
    param(
        [Parameter(Mandatory, ParameterSetName = 'Content', ValueFromPipeline, ValueFromPipelineByPropertyName)][string]$Content,
        [alias('Uri')][Parameter(Mandatory, ParameterSetName = 'Uri')][Uri] $Url,
        [ValidateSet('AngleSharp', 'AgilityPack')] $Engine = 'AgilityPack',
        [switch] $Raw
    )
    Begin {
        $TemporaryProgress = $ProgressPreference
        $ProgressPreference = 'SilentlyContinue'
    }
    Process {
        if ($Engine -eq 'AngleSharp') {
            # Initialize the parser
            $HTMLParser = [AngleSharp.Html.Parser.HtmlParser]::new()
            # Load the html
            if ($Url) {
                $Content = (Invoke-WebRequest -Uri $Url).Content
            }
            if (-not $Content) {
                return
            }
            $ParsedDocument = $HTMLParser.ParseDocument($content)
            if ($Raw) {
                $ParsedDocument
            } else {
                $ParsedDocument.DocumentElement
            }
        } else {
            if ($Content) {
                [HtmlAgilityPack.HtmlDocument] $HtmlDocument = [HtmlAgilityPack.HtmlDocument]::new()
                $HtmlDocument.LoadHtml($Content)
            } else {
                [HtmlAgilityPack.HtmlWeb] $HtmlWeb = [HtmlAgilityPack.HtmlWeb]::new()
                [HtmlAgilityPack.HtmlDocument] $HtmlDocument = $HtmlWeb.Load($url)
            }
            if ($Raw) {
                $HtmlDocument
            } else {
                $HTMLDocument.DocumentNode
            }
        }
    }
    End {
        # Clean up
        $ParsedDocument = $null
        $HtmlDocument = $null
        $HTMLParser = $null
        $ProgressPreference = $TemporaryProgress
    }
}