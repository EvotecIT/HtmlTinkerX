namespace HtmlTinkerX;

/// <summary>
/// How precisely a parsed date string identified a point in time.
/// </summary>
public enum HtmlDatePrecision {
    /// <summary>
    /// The value was not parsed.
    /// </summary>
    None = 0,
    /// <summary>
    /// The value named only a month and year, such as <c>Mar-2026</c> or <c>March 2026</c>; the parsed date is the first of that month.
    /// </summary>
    Month = 1,
    /// <summary>
    /// The value named a specific day, possibly with a time of day.
    /// </summary>
    Day = 2
}
