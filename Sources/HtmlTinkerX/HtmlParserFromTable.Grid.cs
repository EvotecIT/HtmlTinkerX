using AngleSharp.Dom;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HtmlTinkerX;

public static partial class HtmlParserFromTable {
    private static IElement[] GetTableRows(IElement table, bool skipFooter = false) =>
        table.Children.SelectMany(child => {
            if (child.LocalName == "tr") {
                return new[] { child };
            }
            if (child.LocalName == "thead" || child.LocalName == "tbody" || (!skipFooter && child.LocalName == "tfoot")) {
                return child.Children.Where(row => row.LocalName == "tr");
            }
            return Enumerable.Empty<IElement>();
        }).ToArray();

    private static HtmlNodeCollection? GetTableRows(HtmlNode table, bool skipFooter = false) =>
        table.SelectNodes(skipFooter ? "./tr|./thead/tr|./tbody/tr" : "./tr|./thead/tr|./tbody/tr|./tfoot/tr");

    private static IElement[] GetRowCells(IElement row) =>
        row.Children.Where(cell => cell.LocalName == "th" || cell.LocalName == "td").ToArray();

    // HTML spans are unsigned decimal integers. Invalid/zero column spans become one;
    // valid spans saturate at the HTML limits rather than overflowing an Int32.
    private static int ReadColumnSpan(string? value) => Math.Max(1, ReadSpan(value, 1000));

    private static int ReadRowSpan(string? value, int remainingRows) {
        int span = ReadSpan(value, 65534);
        return span == 0 ? remainingRows : Math.Min(span, remainingRows);
    }

    private static int ReadSpan(string? value, int maximum) {
        if (string.IsNullOrWhiteSpace(value)) {
            return 1;
        }
        value = value!.TrimStart();
        int index = value[0] == '+' ? 1 : 0;
        if (index == value.Length || value[index] < '0' || value[index] > '9') {
            return 1;
        }
        int result = 0;
        while (index < value.Length && value[index] >= '0' && value[index] <= '9') {
            result = Math.Min(maximum, result * 10 + value[index++] - '0');
        }
        return result;
    }

    private static int[] GetRemainingGroupRows<T>(IReadOnlyList<T> rows, Func<T, object?> group) {
        var remaining = new int[rows.Count];
        for (int index = rows.Count - 1; index >= 0; index--) {
            remaining[index] = index + 1 < rows.Count && ReferenceEquals(group(rows[index]), group(rows[index + 1]))
                ? remaining[index + 1] + 1 : 1;
        }
        return remaining;
    }

    private static void AddHeader(List<string> headers, string header, int span, TableParseBudget budget) {
        budget.CheckColumns((long)headers.Count + span);
        for (int column = 0; column < span; column++) {
            headers.Add(header);
        }
    }

    private static void SetLinkValue(Dictionary<string, string?> values, string header, string value, int columns, TableParseBudget? budget) {
        if (!values.ContainsKey(header) && budget != null) {
            budget.CheckColumns((long)columns + values.Count + 1);
            budget.AddCells(1, 1);
        }
        values[header] = value;
    }
}
