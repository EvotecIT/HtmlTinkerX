function Format-InternalCSS {
    [CmdletBinding()]
    param(
        [string] $Content
    )
    $CssParser = [AngleSharp.Css.Parser.CssParser]::new()
    $ParsedDocument = $CssParser.ParseStyleSheet($Content)
    $StringWriter = [System.IO.StringWriter]::new()
    $PrettyMarkupFormatter = [AngleSharp.Css.CssStyleFormatter]::new()
    $ParsedDocument.ToCss($StringWriter, $PrettyMarkupFormatter)
    $StringWriter.ToString()
}