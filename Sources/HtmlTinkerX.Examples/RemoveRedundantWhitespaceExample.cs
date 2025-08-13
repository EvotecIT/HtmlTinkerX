using System;

namespace HtmlTinkerX.Examples;

/// <summary>
/// Demonstrates removing redundant whitespace from HTML.
/// </summary>
public static class RemoveRedundantWhitespaceExample {
    /// <summary>Executes the example logic.</summary>
    public static void Run() {
        const string html = "<div>   Hello  </div>  <span>  World</span>";
        string normalized = HtmlUtilities.RemoveRedundantWhitespace(html);
        Console.WriteLine(normalized);
    }
}
