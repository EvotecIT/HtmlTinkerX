function ConvertFrom-HTMLAttributes {
    [alias('ConvertFrom-HTMLTag', 'ConvertFrom-HTMLClass')]
    [cmdletbinding(DefaultParameterSetName = 'Uri')]
    param (
        [Parameter(Mandatory, ParameterSetName = 'Content', ValueFromPipeline, ValueFromPipelineByPropertyName)][string]$Content,
        [alias('Uri')][Parameter(Mandatory, ParameterSetName = 'Uri')][Uri] $Url,
        [string] $Tag,
        [string] $Class,
        [string] $Id,
        [string] $Name,
        [switch] $ReturnObject
    )
    Begin {
        # Initialize the parser
        $TemporaryProgress = $ProgressPreference
        $ProgressPreference = 'SilentlyContinue'
        $HTMLParser = [AngleSharp.Html.Parser.HtmlParser]::new()
    }
    Process {
        # Load the html
        if ($Url) {
            $Content = (Invoke-WebRequest -Uri $Url).Content
        }
        if (-not $Content) {
            return
        }
        $ParsedDocument = $HTMLParser.ParseDocument($Content)
        # Get all the tables
        if ($Tag) {
            [Array] $OutputContent = $ParsedDocument.GetElementsByTagName($Tag)
        } elseif ($Class) {
            [Array] $OutputContent = $ParsedDocument.GetElementsByClassName($Class)
        } elseif ($Id) {
            [Array] $OutputContent = $ParsedDocument.GetElementById($Id)
        } elseif ($Name) {
            [Array] $OutputContent = $ParsedDocument.GetElementsByName($Name)
        }
        if ($OutputContent) {
            if ($ReturnObject) {
                $OutputContent
            } else {
                $OutputContent.TextContent
            }
        }
    }
    End {
        # Clean up
        $ParsedDocument = $null
        $HTMLParser = $null
        $ProgressPreference = $TemporaryProgress
    }
}