using System;
using System.Collections.Generic;
using System.Linq;
using AngleSharp.Diffing.Core;

namespace HtmlTinkerX;

/// <summary>
/// Utility methods for creating an HTML representation of <see cref="IDiff"/> objects.
/// </summary>
public static class HtmlDiffViewer
{
    /// <summary>
    /// Builds HTML table markup for the provided differences.
    /// </summary>
    /// <param name="diffs">Collection of diff objects.</param>
    /// <returns>HTML string.</returns>
    public static string BuildViewerHtml(IEnumerable<IDiff> diffs)
    {
        if (diffs is null)
        {
            throw new ArgumentNullException(nameof(diffs));
        }

        string rows = string.Join(Environment.NewLine,
            diffs.Select(d => $"<tr><td>{d.GetType().Name}</td><td>{d.Target}</td><td>{d.Result}</td></tr>"));

        return $$"""
<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8' />
<title>Diff Viewer</title>
<style>
body { font-family: Arial, sans-serif; }
table { border-collapse: collapse; width: 100%; }
th, td { border: 1px solid #ccc; padding: 4px; text-align: left; }
thead { background: #eee; }
</style>
</head>
<body>
<table>
<thead>
<tr><th>Type</th><th>Target</th><th>Result</th></tr>
</thead>
<tbody>
{{rows}}
</tbody>
</table>
</body>
</html>
""";
    }
}
