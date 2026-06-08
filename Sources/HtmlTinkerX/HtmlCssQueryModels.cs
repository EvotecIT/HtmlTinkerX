namespace HtmlTinkerX;

/// <summary>Represents a CSS rule selected from a style sheet.</summary>
public sealed class HtmlCssRuleMatch {
    /// <summary>Source-order index of the rule.</summary>
    public int Index { get; set; }

    /// <summary>Selector text for style rules, or rule text for non-style rules.</summary>
    public string Selector { get; set; } = string.Empty;

    /// <summary>CSS rule type.</summary>
    public string RuleType { get; set; } = string.Empty;

    /// <summary>Parent media/supports context when the rule is nested.</summary>
    public string? Context { get; set; }

    /// <summary>Raw CSS text for the rule.</summary>
    public string CssText { get; set; } = string.Empty;
}

/// <summary>Represents a CSS declaration selected from a style sheet.</summary>
public sealed class HtmlCssDeclarationMatch {
    /// <summary>Source-order index of the declaration.</summary>
    public int Index { get; set; }

    /// <summary>Selector containing the declaration.</summary>
    public string Selector { get; set; } = string.Empty;

    /// <summary>CSS property name.</summary>
    public string Property { get; set; } = string.Empty;

    /// <summary>CSS property value.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Whether the declaration is marked <c>!important</c>.</summary>
    public bool Important { get; set; }

    /// <summary>Parent media/supports context when the declaration is nested.</summary>
    public string? Context { get; set; }
}

/// <summary>Represents a CSS custom property declaration.</summary>
public sealed class HtmlCssVariableMatch {
    /// <summary>Source-order index of the variable declaration.</summary>
    public int Index { get; set; }

    /// <summary>Selector containing the variable declaration.</summary>
    public string Selector { get; set; } = string.Empty;

    /// <summary>Custom property name, such as <c>--brand-color</c>.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Custom property value.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Parent media/supports context when the variable is nested.</summary>
    public string? Context { get; set; }
}

/// <summary>Represents a URL referenced by CSS.</summary>
public sealed class HtmlCssUrlReference {
    /// <summary>Source-order index of the URL reference.</summary>
    public int Index { get; set; }

    /// <summary>Selector containing the URL, when the URL came from a declaration.</summary>
    public string? Selector { get; set; }

    /// <summary>CSS property containing the URL, or <c>@import</c>.</summary>
    public string Property { get; set; } = string.Empty;

    /// <summary>URL exactly as written in CSS.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>URL resolved against the provided base URL, when possible.</summary>
    public string? ResolvedUrl { get; set; }

    /// <summary>Whether the URL could be parsed or resolved.</summary>
    public bool IsValidUrl { get; set; }

    /// <summary>Parent media/supports context when the URL is nested.</summary>
    public string? Context { get; set; }
}

/// <summary>Represents CSS selector specificity.</summary>
public sealed class HtmlCssSpecificity {
    /// <summary>Selector that was measured.</summary>
    public string Selector { get; set; } = string.Empty;

    /// <summary>Inline specificity component.</summary>
    public int Inline { get; set; }

    /// <summary>ID specificity component.</summary>
    public int Id { get; set; }

    /// <summary>Class, attribute, and pseudo-class specificity component.</summary>
    public int Class { get; set; }

    /// <summary>Element and pseudo-element specificity component.</summary>
    public int Element { get; set; }

    /// <summary>Tuple representation used by CSS specificity documentation.</summary>
    public string Value { get; set; } = string.Empty;
}
