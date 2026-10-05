using System;
using System.Collections.Generic;
using System.Linq;

namespace HtmlTinkerX;

/// <summary>Extracted records, field provenance, and declared quality checks for one document.</summary>
public sealed class HtmlDomExtractionReport {
    /// <summary>CSS selector used to select repeated items.</summary>
    public string ItemSelector { get; set; } = string.Empty;
    /// <summary>Effective document base URL used to resolve relative field URLs.</summary>
    public Uri? BaseUri { get; set; }
    /// <summary>Extracted records. Fields with data errors contain null and have an invalid diagnostic.</summary>
    public IReadOnlyList<HtmlDomExtractionRecord> Records { get; set; } = Array.Empty<HtmlDomExtractionRecord>();
    /// <summary>One diagnostic per declared field and selected item, in extraction order.</summary>
    public IReadOnlyList<HtmlDomFieldDiagnostic> Fields { get; set; } = Array.Empty<HtmlDomFieldDiagnostic>();
    /// <summary>Declared lower item-count bound, when supplied.</summary>
    public int? MinimumItemCount { get; set; }
    /// <summary>Declared upper item-count bound, when supplied.</summary>
    public int? MaximumItemCount { get; set; }
    /// <summary>Number of selected items.</summary>
    public int ItemCount => Records.Count;
    /// <summary>Whether the selected item count satisfies its declared bounds.</summary>
    public bool ItemCountIsValid => (!MinimumItemCount.HasValue || ItemCount >= MinimumItemCount.Value)
        && (!MaximumItemCount.HasValue || ItemCount <= MaximumItemCount.Value);
    /// <summary>Number of fields with a required, count, or conversion error.</summary>
    public int InvalidFieldCount => Fields.Count(diagnostic => !diagnostic.IsValid);
    /// <summary>Number of optional fields for which a default was used.</summary>
    public int DefaultedFieldCount => Fields.Count(diagnostic => diagnostic.Status == HtmlDomFieldStatus.Defaulted);
    /// <summary>Number of fields with no value, including required fields.</summary>
    public int MissingFieldCount => Fields.Count(diagnostic => diagnostic.ValueCount == 0);
    /// <summary>Whether the item count and every field satisfy their declared rules.</summary>
    public bool IsValid => ItemCountIsValid && InvalidFieldCount == 0;
}