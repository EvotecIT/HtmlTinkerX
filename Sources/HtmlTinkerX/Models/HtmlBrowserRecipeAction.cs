namespace HtmlTinkerX;

/// <summary>
/// Browser automation actions supported by an HtmlTinkerX browser recipe.
/// </summary>
public enum HtmlBrowserRecipeAction {
    /// <summary>Navigate the browser to a URL.</summary>
    Navigate,

    /// <summary>Click an element by CSS selector.</summary>
    Click,

    /// <summary>Click an element by visible text.</summary>
    ClickText,

    /// <summary>Fill an input value in one operation.</summary>
    Input,

    /// <summary>Type an input value through keyboard events.</summary>
    TypeInput,

    /// <summary>Set a checkbox or radio element state.</summary>
    SetChecked,

    /// <summary>Select one or more values from a select element.</summary>
    SelectOption,

    /// <summary>Press keyboard keys against a selector.</summary>
    Key,

    /// <summary>Wait for load, selector, JavaScript function, or DOM stability.</summary>
    WaitReady,

    /// <summary>Wait for visible text.</summary>
    WaitText,

    /// <summary>Wait for a fixed number of milliseconds.</summary>
    WaitMilliseconds,

    /// <summary>Evaluate JavaScript on the current page.</summary>
    Script,

    /// <summary>Save a screenshot.</summary>
    Screenshot,

    /// <summary>Export an evidence pack.</summary>
    Evidence,

    /// <summary>Find ranked locator candidates.</summary>
    Locator
}
