function Optimize-InternalUglifyCSS {
    [CmdletBinding()]
    param(
        [string] $Content
    )
    $Settings = [NUglify.Css.CssSettings]::new()
    $Settings.DecodeEscapes = $false
    [NUglify.Uglify]::Css($Content, $Settings).Code
}