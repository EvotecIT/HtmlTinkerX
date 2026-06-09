namespace HtmlTinkerX;

/// <summary>
/// Information about a field inside an HTML form.
/// </summary>
public class HtmlFormField {
    /// <summary>Name attribute of the field.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Type of the field.</summary>
    public HtmlFormFieldType Type { get; set; }

    /// <summary>Current value attribute or element value captured from the field, when present.</summary>
    public string Value { get; set; } = string.Empty;
}
