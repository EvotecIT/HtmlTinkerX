namespace HtmlTinkerX;

/// <summary>Observed outcome for one extracted field.</summary>
public enum HtmlDomFieldStatus {
    /// <summary>A selected value was read and converted.</summary>
    Extracted,
    /// <summary>No value was found and the optional default was used.</summary>
    Defaulted,
    /// <summary>No value or non-null default was found for an optional field.</summary>
    Missing,
    /// <summary>A required field had no value.</summary>
    RequiredMissing,
    /// <summary>The value count fell below the declared minimum.</summary>
    TooFewValues,
    /// <summary>The value count exceeded the declared maximum.</summary>
    TooManyValues,
    /// <summary>A selected value or default could not be converted.</summary>
    InvalidValue
}