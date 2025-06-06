function Format-InternalFormatWithUglify {
    [CmdletBinding()]
    param(
        [string] $Content,
        [string] $Indent = '    ',
        [NUglify.BlockStart] $BlockStartLine = [NUglify.BlockStart]::SameLine,
        [switch] $RemoveOptionalTags,
        [switch] $OutputTextNodesOnNewLine,
        [switch] $RemoveEmptyAttributes,
        [switch] $AlphabeticallyOrderAttributes,
        [switch] $RemoveEmptyBlocks,
        [switch] $RemoveComments,
        [switch] $IsFragment
    )
    $Settings = [NUglify.Html.HtmlSettings]::new()
    # HTML Settings
    if ($IsFragment) {
        $Settings.IsFragmentOnly = $true
    }
    # Keep first comment
    # $Pattern = "<!-- saved from url=\(0014\)about:internet -->"
    # $MOTW = [System.Text.RegularExpressions.Regex]::new($Pattern) #, [System.Text.RegularExpressions.RegexOptions]::MultiLine)
    # $Settings.KeepCommentsRegex.Add($MOTW)

    $Settings.RemoveOptionalTags = $RemoveOptionalTags.IsPresent
    $Settings.PrettyPrint = $true
    $Settings.Indent = $Indent
    $Settings.OutputTextNodesOnNewLine = $OutputTextNodesOnNewLine.IsPresent # option to not indent textnodes when theyre the only child
    $Settings.RemoveEmptyAttributes = $RemoveEmptyAttributes.IsPresent
    $Settings.AlphabeticallyOrderAttributes = $AlphabeticallyOrderAttributes.IsPresent
    $Settings.RemoveComments = $RemoveComments
    $Settings.RemoveQuotedAttributes = $false
    #$Settings.LineTerminator = [System.Environment]::NewLine
    # JS Settings
    $Settings.JsSettings.MinifyCode = $true
    $Settings.JsSettings.OutputMode = [NUglify.OutputMode]::MultipleLines
    $Settings.JsSettings.Indent = $Indent
    $Settings.JsSettings.BlocksStartOnSameLine = $BlockStartLine
    $Settings.JsSettings.PreserveFunctionNames = $true
    $Settings.JsSettings.LocalRenaming = [NUglify.JavaScript.LocalRenaming]::KeepAll
    #$Settings.JsSettings.EvalTreatment = [NUglify.JavaScript.EvalTreatment]::Ignore
    #$Settings.JsSettings.Format = [NUglify.JavaScript.JavaScriptFormat]::Normal
    $Settings.JsSettings.NoAutoRenameList = $true
    $Settings.JsSettings.PreserveFunctionNames = $true
    #$Settings.JsSettings.CollapseToLiteral = $true
    #$Settings.JsSettings.ConstStatementsMozilla = $true
    #$Settings.JsSettings.LineBreakThreshold = 50
    $Settings.JsSettings.ReorderScopeDeclarations = $false
    #$Settings.JsSettings.RenamePairs = $false
    #$Settings.JsSettings.QuoteObjectLiteralProperties = $true
    $Settings.JsSettings.TermSemicolons = $true
    #$Settings.JsSettings.Format = [NUglify.JavaScript.JavaScriptFormat]::Normal
    $Settings.JsSettings.RemoveUnneededCode = $false;
    $Settings.JsSettings.RemoveFunctionExpressionNames = $false;
    # $Settings.NoAutoRenameCollection  # ReadOnly
    #$Settings.JsSettings.LineTerminator = "`r`n"
    # CSS Settings
    $Settings.CssSettings.OutputMode = [NUglify.OutputMode]::MultipleLines
    $Settings.CssSettings.Indent = $Indent
    $Settings.CssSettings.BlocksStartOnSameLine = $BlockStartLine
    $Settings.CssSettings.RemoveEmptyBlocks = $RemoveEmptyBlocks
    $Settings.CssSettings.DecodeEscapes = $false
    #$Settings.CssSettings.LineTerminator = "`r`n"
    [NUglify.Uglify]::Html($Content, $Settings).Code
}
