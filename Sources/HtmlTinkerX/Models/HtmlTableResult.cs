using AngleSharp.Dom;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Result of table parsing with metadata.
/// </summary>
public class HtmlTableResult {
    /// <summary>
    /// Metadata describing the parsed table.
    /// </summary>
    public HtmlTableMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Table rows with values indexed by header name.
    /// </summary>
    public List<HtmlTableRow> Data { get; set; } = new();
}