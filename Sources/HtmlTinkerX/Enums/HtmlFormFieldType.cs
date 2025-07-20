namespace HtmlTinkerX;

/// <summary>
/// Describes the type of an HTML form field.
/// </summary>
public enum HtmlFormFieldType {
    /// <summary>Input element of type "text".</summary>
    Text,
    /// <summary>Input element of type "password".</summary>
    Password,
    /// <summary>Input element of type "hidden".</summary>
    Hidden,
    /// <summary>Input element of type "checkbox".</summary>
    Checkbox,
    /// <summary>Input element of type "radio".</summary>
    Radio,
    /// <summary>Input element of type "submit".</summary>
    Submit,
    /// <summary>Select element.</summary>
    Select,
    /// <summary>Textarea element.</summary>
    Textarea,
    /// <summary>Button element.</summary>
    Button,
    /// <summary>Other or unrecognized element type.</summary>
    Other
}
