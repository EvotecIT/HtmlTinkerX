using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AngleSharp.Dom;
using HtmlAgilityPack;

namespace PSParseHTML;

/// <summary>
/// Result of table parsing with metadata.
/// </summary>
public class TableParseResult {
    public TableMetadata Metadata { get; set; } = new();
    public List<Dictionary<string, string?>> Data { get; set; } = new();
}