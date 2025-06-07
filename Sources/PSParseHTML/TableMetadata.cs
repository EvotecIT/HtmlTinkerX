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
public class TableMetadata {
    public int TableIndex { get; set; }
    public string? Id { get; set; }
    public string? Classes { get; set; }
    public Dictionary<string, string> Attributes { get; set; } = new();
    public int RowCount { get; set; }
    public int ColumnCount { get; set; }
    public List<string> Headers { get; set; } = new();
    public bool IsVisible { get; set; } = true;
}