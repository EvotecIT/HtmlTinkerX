function Format-InternalJS {
    [CmdletBinding()]
    param(
        [string] $Content,
        [int] $IndentSize = 4,
        [string] $IndentChar = ' ',
        [bool] $IndentWithTabs = $false,
        [bool] $PreserveNewlines = $true,
        [double] $MaxPreserveNewlines = 10.0,
        [bool] $JslintHappy = $false,
        [Jsbeautifier.BraceStyle] $BraceStyle = [Jsbeautifier.BraceStyle]::Collapse,
        [bool] $KeepArrayIndentation = $false,
        [bool] $KeepFunctionIndentation = $false,
        [bool] $EvalCode = $false,
        #[int] $WrapLineLength = 0,
        [bool] $BreakChainedMethods = $false
    )
    $Jsbeautifier = [Jsbeautifier.Beautifier]::new()
    $Jsbeautifier.Opts.IndentSize = $IndentSize
    $Jsbeautifier.Opts.IndentChar = $IndentChar
    $Jsbeautifier.Opts.IndentWithTabs = $IndentWithTabs
    $Jsbeautifier.Opts.PreserveNewlines = $PreserveNewlines
    $Jsbeautifier.Opts.MaxPreserveNewlines = $MaxPreserveNewlines
    $Jsbeautifier.Opts.JslintHappy = $JslintHappy
    $Jsbeautifier.Opts.BraceStyle = $BraceStyle
    $Jsbeautifier.Opts.KeepArrayIndentation = $KeepArrayIndentation
    $Jsbeautifier.Opts.KeepFunctionIndentation = $KeepFunctionIndentation
    $Jsbeautifier.Opts.EvalCode = $EvalCode
    #$Jsbeautifier.Opts.WrapLineLength = $WrapLineLength
    $Jsbeautifier.Opts.BreakChainedMethods = $BreakChainedMethods

    #$Jsbeautifier.Flags
    <#
    public BeautifierFlags(string mode)
    {
        PreviousMode = "BLOCK";
        Mode = mode;
        VarLine = false;
        VarLineTainted = false;
        VarLineReindented = false;
        InHtmlComment = false;
        IfLine = false;
        ChainExtraIndentation = 0;
        InCase = false;
        InCaseStatement = false;
        CaseBody = false;
        IndentationLevel = 0;
        TernaryDepth = 0;
    }
    #>
    $FormattedJS = $Jsbeautifier.Beautify($Content)
    $FormattedJS
}