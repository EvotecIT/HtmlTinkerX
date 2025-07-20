namespace HtmlTinkerX.JavaScriptBeautifier;

/// <summary>
/// Defines the parser modes used by the JavaScript beautifier.
/// </summary>
public enum BeautifierMode {
    /// <summary>
    /// Default block mode.
    /// </summary>
    Block,

    /// <summary>
    /// Mode for do/while blocks.
    /// </summary>
    DoBlock,

    /// <summary>
    /// Object literal parsing mode.
    /// </summary>
    Object,

    /// <summary>
    /// Expression within square brackets.
    /// </summary>
    ArrayExpression,

    /// <summary>
    /// Indented expression within square brackets.
    /// </summary>
    IndentedArrayExpression,

    /// <summary>
    /// Expression within parentheses.
    /// </summary>
    Expression,

    /// <summary>
    /// Expression within a for loop.
    /// </summary>
    ForExpression,

    /// <summary>
    /// Expression within a conditional statement.
    /// </summary>
    CondExpression
}
