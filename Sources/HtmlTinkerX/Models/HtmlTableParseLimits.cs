using System;
using System.IO;

namespace HtmlTinkerX;

/// <summary>Limits the materialized table results from one parsing operation.</summary>
public sealed class HtmlTableParseLimits {
    /// <summary>Default maximum number of output columns in one table.</summary>
    public const int DefaultMaximumColumns = 10000;
    /// <summary>Default maximum number of source rows across all tables.</summary>
    public const int DefaultMaximumRows = 100000;
    /// <summary>Default maximum number of expanded cells across all tables.</summary>
    public const long DefaultMaximumExpandedCells = 1000000;

    /// <summary>Maximum columns per table, including companion link columns.</summary>
    public int MaximumColumns { get; set; } = DefaultMaximumColumns;
    /// <summary>Maximum source rows across all tables, including headers.</summary>
    public int MaximumRows { get; set; } = DefaultMaximumRows;
    /// <summary>Maximum expanded result cells across all tables.</summary>
    public long MaximumExpandedCells { get; set; } = DefaultMaximumExpandedCells;

    internal TableParseBudget CreateBudget() {
        if (MaximumColumns <= 0) {
            throw new ArgumentOutOfRangeException(nameof(MaximumColumns));
        }
        if (MaximumRows <= 0) {
            throw new ArgumentOutOfRangeException(nameof(MaximumRows));
        }
        if (MaximumExpandedCells <= 0) {
            throw new ArgumentOutOfRangeException(nameof(MaximumExpandedCells));
        }
        return new TableParseBudget(MaximumColumns, MaximumRows, MaximumExpandedCells);
    }
}

internal sealed class TableParseBudget {
    private readonly int maximumColumns;
    private readonly int maximumRows;
    private readonly long maximumCells;
    private long rows;
    private long cells;

    internal TableParseBudget(int maximumColumns, int maximumRows, long maximumCells) {
        this.maximumColumns = maximumColumns;
        this.maximumRows = maximumRows;
        this.maximumCells = maximumCells;
    }

    internal void CheckColumns(long count) {
        if (count > maximumColumns) {
            throw new InvalidDataException($"Table exceeds the maximum of {maximumColumns} columns.");
        }
    }

    internal void AddRows(int count) {
        if (count > maximumRows - rows) {
            throw new InvalidDataException($"Tables exceed the maximum of {maximumRows} source rows.");
        }
        rows += count;
    }

    internal void AddCells(int rowCount, int columnCount) {
        CheckColumns(columnCount);
        long count = (long)rowCount * columnCount;
        if (count > maximumCells - cells) {
            throw new InvalidDataException($"Tables exceed the maximum of {maximumCells} expanded cells.");
        }
        cells += count;
    }
}
