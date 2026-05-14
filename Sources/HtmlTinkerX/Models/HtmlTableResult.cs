using System.Collections.Generic;
using System.Data;

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
    public List<Dictionary<string, string?>> Data { get; set; } = new();

    /// <summary>
    /// Converts this parsed HTML table into a <see cref="DataTable"/>.
    /// </summary>
    /// <param name="tableName">Optional table name. When omitted, a stable name is derived from table metadata.</param>
    /// <param name="inferTypes">When true, columns with consistently typed text are converted to simple .NET types.</param>
    public DataTable ToDataTable(string? tableName = null, bool inferTypes = false) =>
        HtmlTableDataExtensions.ToDataTable(this, tableName, inferTypes);
}
