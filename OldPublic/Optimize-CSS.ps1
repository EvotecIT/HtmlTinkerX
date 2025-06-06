function Optimize-CSS {
    [CmdletBinding()]
    param(
        [string] $File,
        [string] $OutputFile,
        [string] $Content
    )
    # Load from file or text
    if ($File) {
        if (Test-Path -LiteralPath $File) {
            $Content = [IO.File]::ReadAllText($File)
        } else {
            Write-Warning "Optimize-CSS - File doesn't exists"
            return
        }
    } elseif ($Content) {

    } else {
        Write-Warning 'Optimize-CSS - No choice file or Content. Termninated.'
        return
    }

    # Do magic
    #$Output = Optimize-InternalCSS -Content $Content
    $Output = Optimize-InternalUglifyCSS -Content $Content

    # Output to file or to text
    if ($OutputFile) {
        [IO.File]::WriteAllText($OutputFile, $Output)
    } else {
        $Output
    }
}