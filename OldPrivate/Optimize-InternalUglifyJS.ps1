function Optimize-InternalUglifyJS {
    [CmdletBinding()]
    param(
        [string] $Content,
        [string] $Indent = '    '
    )
    #$Settings = [NUglify.JavaScript.CodeSettings]::new()
    #$Settings.MinifyCode = $true
    #$Settings.OutputMode = [NUglify.OutputMode]::MultipleLines
    #$Settings.Indent = $Indent
    #$Settings.JsSettings.MinifyCode = $true
    #$Settings.JsSettings.OutputMode = [NUglify.OutputMode]::MultipleLines
    #$Settings.JsSettings.Indent = $Indent
    #$Settings.JsSettings.BlocksStartOnSameLine = $BlockStartLine
    [NUglify.Uglify]::Js($Content).Code
}