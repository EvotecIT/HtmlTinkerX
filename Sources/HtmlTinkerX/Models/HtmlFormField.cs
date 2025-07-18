namespace HtmlTinkerX;

/// <summary>
/// Information about a field inside an HTML form.
/// </summary>
public class HtmlFormField {
    /// <summary>Name attribute of the field.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Type of the field (text, password, select, etc.).</summary>
    public string Type { get; set; } = string.Empty;
}
