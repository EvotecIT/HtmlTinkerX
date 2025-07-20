using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Represents a single table row with column values.
/// </summary>
public class HtmlTableRow {
    /// <summary>Column values indexed by header name.</summary>
    public Dictionary<string, string?> Values { get; set; } = new();
}
