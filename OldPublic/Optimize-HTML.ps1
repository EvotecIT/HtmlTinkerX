function Optimize-HTML {
    [CmdletBinding()]
    param(
        [string] $File,
        [string] $OutputFile,
        [string] $Content,
        [switch] $CSSDecodeEscapes
    )
    # Load from file or text
    if ($File) {
        if (Test-Path -LiteralPath $File) {
            $Content = [IO.File]::ReadAllText($File)
        } else {
            Write-Warning "Optimize-HTML - File doesn't exists"
            return
        }
    } elseif ($Content) {

    } else {
        Write-Warning 'Optimize-HTML - No choice file or Content. Termninated.'
        return
    }

    # Do magic
    $Output = Optimize-InternalUglifyHTML -Content $Content -CSSDecodeEscapes:$CSSDecodeEscapes

    # Output to file or to text
    if ($OutputFile) {
        [IO.File]::WriteAllText($OutputFile, $Output)
    } else {
        $Output
    }
}