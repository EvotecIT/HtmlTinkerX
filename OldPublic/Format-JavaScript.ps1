function Format-JavaScript {
    [alias('Format-JS')]
    [CmdletBinding()]
    param(
        [string] $File,
        [string] $OutputFile,
        [alias('FileContent')][string] $Content
    )
    # Load from file or text
    if ($File) {
        if (Test-Path -LiteralPath $File) {
            $Content = [IO.File]::ReadAllText($File)
        } else {
            Write-Warning "Format-JavaScript - File doesn't exists"
            return
        }
    } elseif ($Content) {

    } else {
        Write-Warning 'Format-JavaScript - No choice file or Content. Termninated.'
        return
    }

    # For now don't want to give this as an option
    [int] $IndentSize = 4
    [string] $IndentChar = ' '
    [bool] $IndentWithTabs = $false
    [bool] $PreserveNewlines = $true
    [double] $MaxPreserveNewlines = 10.0
    [bool] $JslintHappy = $false
    [Jsbeautifier.BraceStyle] $BraceStyle = [Jsbeautifier.BraceStyle]::Collapse
    [bool] $KeepArrayIndentation = $false
    [bool] $KeepFunctionIndentation = $false
    [bool] $EvalCode = $false
    #[int] $WrapLineLength = 0
    [bool] $BreakChainedMethods = $false

    # do the magic
    $SplatJS = @{
        IndentSize              = $IndentSize
        IndentChar              = $IndentChar
        IndentWithTabs          = $IndentWithTabs
        PreserveNewlines        = $PreserveNewlines
        MaxPreserveNewlines     = $MaxPreserveNewlines
        JslintHappy             = $JslintHappy
        BraceStyle              = $BraceStyle
        KeepArrayIndentation    = $KeepArrayIndentation
        KeepFunctionIndentation = $KeepFunctionIndentation
        EvalCode                = $EvalCode
        #WrapLineLength          = $WrapLineLength
        BreakChainedMethods     = $BreakChainedMethods
    }

    $Output = Format-InternalJS -Content $Content @SplatJS
    #$Output = Format-InternalUglifyJS -Content $Content
    if ($OutputFile) {
        [IO.File]::WriteAllText($OutputFile, $Output)
    } else {
        $Output
    }

    <#
    $IndentLenght = $Indent.Length
    $Content = "<script>$Content</script>"
    $Output = Format-InternalFormatWithUglify -Content $Content -IsFragment
    $SplitOutput = ($Output.Split("`n"))
    $NewOutput = for ($i = 1; $i -lt $SplitOutput.Count - 1; $i++) {
        $SplitOutput[$i].SubString($IndentLenght)
    }
    $FinalOutput = $NewOutput -join "`n"
    # Output to file or to text
    if ($OutputFile) {
        [IO.File]::WriteAllText($OutputFile, $FinalOutput)
    } else {
        $FinalOutput
    }
    #>
}