function Format-CSS {
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
            Write-Warning "Format-CSS - File doesn't exists"
            return
        }
    } elseif ($Content) {

    } else {
        Write-Warning 'Format-CSS - No choice file or Content. Termninated.'
        return
    }

    $Output = Format-InternalCSS -Content $Content
    #$Content = "<style>$Content</style>"
    #$Output = Format-InternalFormatWithUglify -Content $Content -IsFragment

    # Output to file or to text
    if ($OutputFile) {
        [IO.File]::WriteAllText($OutputFile, $Output)
    } else {
        $Output
    }
}