using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace HtmlTinkerX;

/// <summary>Accepted extraction structure saved with a recipe, without extracted values or raw content.</summary>
public sealed class HtmlExtractionBaseline {
    /// <summary>Baseline schema version.</summary>
    public int Version { get; set; } = 1;
    /// <summary>Source kind to which this baseline belongs.</summary>
    public string SourceKind { get; set; } = string.Empty;
    /// <summary>Number of accepted items. A changed count is reported; declared count rules decide validity.</summary>
    public int ItemCount { get; set; }
    /// <summary>Distinct accepted item structures, including source paths and value kinds, without values.</summary>
    public IReadOnlyList<string> ItemShapes { get; set; } = Array.Empty<string>();
    /// <summary>JSON response envelope structure. Record array contents are compared through ItemShapes.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ResponseShape { get; set; }
    /// <summary>Accepted High, Medium or Low structure confidence for a DOM recipe.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CollectionConfidence { get; set; }
}

/// <summary>Comparison with a recipe's accepted structure. Ordinary value changes are ignored.</summary>
public sealed class HtmlExtractionDriftReport {
    /// <summary>Number of items in the accepted extraction.</summary>
    public int BaselineItemCount { get; set; }
    /// <summary>Number of items in the current extraction.</summary>
    public int CurrentItemCount { get; set; }
    /// <summary>Structures observed now that were absent from the accepted extraction.</summary>
    public IReadOnlyList<string> AddedShapes { get; set; } = Array.Empty<string>();
    /// <summary>Accepted structures no longer observed.</summary>
    public IReadOnlyList<string> MissingShapes { get; set; } = Array.Empty<string>();
    /// <summary>Accepted collection discovery confidence, when captured.</summary>
    public string? BaselineConfidence { get; set; }
    /// <summary>Current collection discovery confidence, when applicable.</summary>
    public string? CurrentConfidence { get; set; }
    /// <summary>Whether the item count changed. Declared count bounds remain authoritative.</summary>
    public bool ItemCountChanged => BaselineItemCount != CurrentItemCount;
    /// <summary>Whether the set of item structures changed.</summary>
    public bool ShapeChanged => ResponseShapeChanged || AddedShapes.Count != 0 || MissingShapes.Count != 0;
    /// <summary>Whether the JSON structure outside extracted records changed.</summary>
    public bool ResponseShapeChanged { get; set; }
    /// <summary>Comparison capacity failure, when current data cannot be compared completely. Records remain available.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ComparisonError { get; set; }
    /// <summary>Whether discovery confidence fell below the accepted level.</summary>
    public bool ConfidenceReduced { get; set; }
    /// <summary>Whether any structure, count or confidence change was observed.</summary>
    public bool HasChanges => ItemCountChanged || ShapeChanged || ConfidenceReduced;
    /// <summary>Whether shapes and confidence remain compatible; count rules are checked separately.</summary>
    public bool IsCompatible => ComparisonError == null && !ShapeChanged && !ConfidenceReduced;
}
