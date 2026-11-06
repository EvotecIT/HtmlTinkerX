namespace HtmlTinkerX;

/// <summary>
/// Controls how an element's own text is compared when finding elements by text.
/// </summary>
public enum HtmlTextMatch {
    /// <summary>
    /// The whitespace-normalized own text equals the search text.
    /// </summary>
    Exact = 0,
    /// <summary>
    /// The own text contains the search text.
    /// </summary>
    Contains = 1,
    /// <summary>
    /// The own text matches the search text as a regular expression.
    /// </summary>
    Regex = 2
}
