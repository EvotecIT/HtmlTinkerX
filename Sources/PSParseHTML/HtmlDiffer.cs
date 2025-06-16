using System;
using System.Collections.Generic;
using AngleSharp.Diffing;
using AngleSharp.Diffing.Core;

namespace PSParseHTML;

/// <summary>
/// Provides helpers for comparing HTML markup using AngleSharp.Diffing.
/// </summary>
public static class HtmlDiffer {
    /// <summary>
    /// Compares two HTML fragments and returns the differences.
    /// </summary>
    /// <param name="reference">Reference HTML markup.</param>
    /// <param name="difference">Markup to compare against the reference.</param>
    /// <returns>Collection of differences.</returns>
    public static IEnumerable<IDiff> Compare(string reference, string difference) {
        if (reference == null) {
            throw new ArgumentNullException(nameof(reference));
        }
        if (difference == null) {
            throw new ArgumentNullException(nameof(difference));
        }

        return DiffBuilder
            .Compare(reference)
            .WithTest(difference)
            .Build();
    }
}
