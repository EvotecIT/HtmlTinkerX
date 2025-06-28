using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AngleSharp.Dom;
using HtmlAgilityPack;

namespace PSParseHTML;

/// <summary>
/// Metadata about a parsed table.
/// </summary>
public class HtmlTableMetadata {
    /// <summary>
    /// Index of the table within the document.
    /// </summary>
    public int TableIndex { get; set; }

    /// <summary>
    /// Indicates whether the table is visible.
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Value of the id attribute on the table element.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// CSS classes applied to the table element.
    /// </summary>
    public string? Classes { get; set; }

    /// <summary>
    /// Additional attributes found on the table element.
    /// </summary>
    public Dictionary<string, string> Attributes { get; set; } = new();

    /// <summary>
    /// Number of rows detected in the table.
    /// </summary>
    public int RowCount { get; set; }

    /// <summary>
    /// Number of columns detected in the table.
    /// </summary>
    public int ColumnCount { get; set; }

    /// <summary>
    /// Table header names, if any were detected.
    /// </summary>
    public List<string> Headers { get; set; } = new();
}