function Format-HTML {
    [CmdletBinding()]
    param(
        [string] $File,
        [string] $OutputFile,
        [string] $Content,
        [string] $Indent = '    ',
        [NUglify.BlockStart] $BlockStartLine = [NUglify.BlockStart]::SameLine,
        [switch] $RemoveHTMLComments,
        [switch] $RemoveOptionalTags,
        [switch] $OutputTextNodesOnNewLine,
        [switch] $RemoveEmptyAttributes,
        [switch] $AlphabeticallyOrderAttributes,
        [switch] $RemoveEmptyBlocks
    )

    # Load from file or text
    if ($File) {
        if (Test-Path -LiteralPath $File) {
            $Content = [IO.File]::ReadAllText($File)
        } else {
            Write-Warning "Format-HTML - File doesn't exists"
            return
        }
    } elseif ($Content) {

    } else {
        Write-Warning 'Format-HTML - No choice file or Content. Termninated.'
        return
    }

    # Do the magic
    $formatInternalFormatWithUglifySplat = @{
        Content                       = $Content
        Indent                        = $Indent
        BlockStartLine                = $BlockStartLine
        OutputTextNodesOnNewLine      = $OutputTextNodesOnNewLine
        RemoveOptionalTags            = $RemoveOptionalTags
        RemoveEmptyAttributes         = $RemoveEmptyAttributes
        AlphabeticallyOrderAttributes = $AlphabeticallyOrderAttributes
        RemoveEmptyBlocks             = $RemoveEmptyBlocks
        RemoveComments                = $RemoveHTMLComments
        #IsFragment                    = $true
    }
    $Output = Format-InternalFormatWithUglify @formatInternalFormatWithUglifySplat

    # Output to file or to text
    if ($OutputFile) {
        [IO.File]::WriteAllText($OutputFile, $Output)
    } else {
        $Output
    }
}