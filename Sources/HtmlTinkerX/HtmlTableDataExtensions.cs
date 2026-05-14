using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;

namespace HtmlTinkerX;

/// <summary>
/// Converts parsed HTML table models into common .NET tabular shapes.
/// </summary>
public static class HtmlTableDataExtensions {
    private const string DefaultColumnNamePrefix = "Column";
    private const string DefaultDataSetName = "HtmlTables";

    /// <summary>
    /// Converts a parsed HTML table into a <see cref="DataTable"/>.
    /// </summary>
    /// <param name="table">Parsed table.</param>
    /// <param name="tableName">Optional table name. When omitted, a stable name is derived from table metadata.</param>
    /// <param name="inferTypes">When true, columns with consistently typed text are converted to simple .NET types.</param>
    public static DataTable ToDataTable(this HtmlTableResult table, string? tableName = null, bool inferTypes = false) {
        if (table == null) {
            throw new ArgumentNullException(nameof(table));
        }

        List<string> sourceHeaders = GetSourceHeaders(table);
        List<string> outputHeaders = sourceHeaders.ToList();
        HtmlParser.EnsureUniqueNames(outputHeaders);

        Type[] columnTypes = inferTypes
            ? sourceHeaders.Select(header => InferColumnType(table.Data, header)).ToArray()
            : sourceHeaders.Select(_ => typeof(string)).ToArray();

        var dataTable = new DataTable(SanitizeTableName(tableName ?? GetDefaultTableName(table.Metadata, 1)));
        for (int i = 0; i < outputHeaders.Count; i++) {
            var column = new DataColumn(outputHeaders[i], columnTypes[i]) {
                AllowDBNull = true
            };
            dataTable.Columns.Add(column);
        }

        foreach (Dictionary<string, string?> sourceRow in table.Data) {
            DataRow row = dataTable.NewRow();
            for (int i = 0; i < sourceHeaders.Count; i++) {
                string? value = TryGetValue(sourceRow, sourceHeaders[i]);
                row[i] = ConvertValue(value, columnTypes[i]);
            }

            dataTable.Rows.Add(row);
        }

        return dataTable;
    }

    /// <summary>
    /// Converts parsed HTML tables into a <see cref="DataSet"/> with one table per parsed HTML table.
    /// </summary>
    /// <param name="tables">Parsed tables.</param>
    /// <param name="dataSetName">Optional DataSet name.</param>
    /// <param name="inferTypes">When true, columns with consistently typed text are converted to simple .NET types.</param>
    public static DataSet ToDataSet(this IEnumerable<HtmlTableResult> tables, string? dataSetName = null, bool inferTypes = false) {
        if (tables == null) {
            throw new ArgumentNullException(nameof(tables));
        }

        var dataSet = new DataSet(string.IsNullOrWhiteSpace(dataSetName) ? DefaultDataSetName : dataSetName);
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int index = 1;
        foreach (HtmlTableResult table in tables) {
            if (table == null) {
                continue;
            }

            string tableName = EnsureUniqueTableName(GetDefaultTableName(table.Metadata, index), usedNames);
            DataTable dataTable = table.ToDataTable(tableName, inferTypes);
            dataSet.Tables.Add(dataTable);
            index++;
        }

        return dataSet;
    }

    /// <summary>
    /// Selects parsed HTML tables by index, id, class, caption text, or header.
    /// </summary>
    /// <param name="tables">Parsed tables.</param>
    /// <param name="options">Selection options. Empty options return all tables.</param>
    public static List<HtmlTableResult> SelectTables(this IEnumerable<HtmlTableResult> tables, HtmlTableSelectionOptions? options) {
        if (tables == null) {
            throw new ArgumentNullException(nameof(tables));
        }

        if (options == null) {
            return tables.Where(table => table != null).ToList();
        }

        var comparison = options.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var indexSet = options.TableIndexes?.Count > 0
            ? new HashSet<int>(options.TableIndexes.Where(index => index >= 0))
            : null;

        if (!HasSelectionCriteria(options, indexSet)) {
            return tables.Where(table => table != null).ToList();
        }

        return tables
            .Where(table => table != null)
            .Where(table => MatchesSelection(table, options, indexSet, comparison))
            .ToList();
    }

    private static List<string> GetSourceHeaders(HtmlTableResult table) {
        var headers = table.Metadata.Headers
            .Where(header => !string.IsNullOrWhiteSpace(header))
            .ToList();

        if (headers.Count == 0) {
            foreach (Dictionary<string, string?> row in table.Data) {
                foreach (string key in row.Keys) {
                    if (!headers.Contains(key, StringComparer.OrdinalIgnoreCase)) {
                        headers.Add(key);
                    }
                }
            }
        }

        if (headers.Count == 0) {
            headers.Add(DefaultColumnNamePrefix + "1");
        }

        for (int i = 0; i < headers.Count; i++) {
            if (string.IsNullOrWhiteSpace(headers[i])) {
                headers[i] = DefaultColumnNamePrefix + (i + 1).ToString(CultureInfo.InvariantCulture);
            }
        }

        return headers;
    }

    private static string? TryGetValue(Dictionary<string, string?> row, string header) {
        if (row.TryGetValue(header, out string? value)) {
            return value;
        }

        foreach (KeyValuePair<string, string?> pair in row) {
            if (string.Equals(pair.Key, header, StringComparison.OrdinalIgnoreCase)) {
                return pair.Value;
            }
        }

        return null;
    }

    private static Type InferColumnType(IEnumerable<Dictionary<string, string?>> rows, string header) {
        List<string> values = rows
            .Select(row => TryGetValue(row, header))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToList();

        if (values.Count == 0) {
            return typeof(string);
        }

        if (values.All(value => bool.TryParse(value, out _))) {
            return typeof(bool);
        }

        if (values.All(value => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))) {
            return typeof(long);
        }

        if (values.All(value => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _))) {
            return typeof(decimal);
        }

        if (values.All(value => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out _))) {
            return typeof(DateTime);
        }

        return typeof(string);
    }

    private static object ConvertValue(string? value, Type targetType) {
        if (string.IsNullOrWhiteSpace(value)) {
            return DBNull.Value;
        }

        string text = value!.Trim();
        if (targetType == typeof(bool) && bool.TryParse(text, out bool boolValue)) {
            return boolValue;
        }

        if (targetType == typeof(long) && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long longValue)) {
            return longValue;
        }

        if (targetType == typeof(decimal) && decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal decimalValue)) {
            return decimalValue;
        }

        if (targetType == typeof(DateTime) && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out DateTime dateValue)) {
            return dateValue;
        }

        return value;
    }

    private static string GetDefaultTableName(HtmlTableMetadata metadata, int fallbackIndex) {
        if (!string.IsNullOrWhiteSpace(metadata.Id)) {
            return metadata.Id!;
        }

        if (!string.IsNullOrWhiteSpace(metadata.Classes)) {
            string className = metadata.Classes!
                .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(className)) {
                return className;
            }
        }

        int tableIndex = metadata.TableIndex >= 0 ? metadata.TableIndex + 1 : fallbackIndex;
        return "Table" + tableIndex.ToString(CultureInfo.InvariantCulture);
    }

    private static string EnsureUniqueTableName(string requestedName, ISet<string> usedNames) {
        string baseName = SanitizeTableName(requestedName);
        string candidate = baseName;
        int suffix = 2;
        while (!usedNames.Add(candidate)) {
            candidate = baseName + suffix.ToString(CultureInfo.InvariantCulture);
            suffix++;
        }

        return candidate;
    }

    private static string SanitizeTableName(string name) {
        if (string.IsNullOrWhiteSpace(name)) {
            return "Table";
        }

        var builder = new StringBuilder(name.Length);
        foreach (char ch in name.Trim()) {
            builder.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
        }

        string sanitized = builder.ToString().Trim('_');
        if (sanitized.Length == 0) {
            sanitized = "Table";
        }

        if (char.IsDigit(sanitized[0])) {
            sanitized = "_" + sanitized;
        }

        return sanitized;
    }

    private static bool HasSelectionCriteria(HtmlTableSelectionOptions options, ISet<int>? indexSet) {
        return indexSet?.Count > 0 ||
               !string.IsNullOrWhiteSpace(options.Id) ||
               !string.IsNullOrWhiteSpace(options.ClassName) ||
               !string.IsNullOrWhiteSpace(options.CaptionContains) ||
               !string.IsNullOrWhiteSpace(options.Header);
    }

    private static bool MatchesSelection(
        HtmlTableResult table,
        HtmlTableSelectionOptions options,
        ISet<int>? indexSet,
        StringComparison comparison) {
        var metadata = table.Metadata;
        if (indexSet != null && !indexSet.Contains(metadata.TableIndex)) {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(options.Id) &&
            !string.Equals(metadata.Id, options.Id, comparison)) {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(options.ClassName) &&
            !HasClassToken(metadata.Classes, options.ClassName!, comparison)) {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(options.CaptionContains) &&
            (metadata.Caption == null || metadata.Caption.IndexOf(options.CaptionContains!, comparison) < 0)) {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(options.Header) &&
            !metadata.Headers.Any(header => string.Equals(header, options.Header, comparison))) {
            return false;
        }

        return true;
    }

    private static bool HasClassToken(string? classes, string className, StringComparison comparison) {
        if (string.IsNullOrWhiteSpace(classes)) {
            return false;
        }

        return classes.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Any(token => string.Equals(token, className, comparison));
    }
}
