function Convert-InternalHTMLToText {
    [CmdletBinding()]
    param(
        [string] $Content
    )
    $Output = [NUglify.Uglify]::HtmlToText($Content)
    if ($Output.HasErrors) {
        Write-Warning "Convert-HTMLToText -Errors: $($Output.Errors)"
    }
    $Output.Code
}