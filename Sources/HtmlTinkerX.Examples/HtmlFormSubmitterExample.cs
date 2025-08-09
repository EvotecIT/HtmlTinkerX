using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HtmlTinkerX.Examples;

/// <summary>
/// Demonstrates submitting a form using an HTTP GET request while preserving existing query parameters.
/// </summary>
public static class HtmlFormSubmitterExample {
    /// <summary>
    /// Executes the example logic.
    /// </summary>
    public static async Task RunAsync() {
        var fields = new Dictionary<string, string> {
            ["user"] = "admin",
            ["pass"] = "secret"
        };

        string url = "https://httpbin.org/get?existing=value";
        string result = await HtmlFormSubmitter.SubmitAsync(url, FormMethod.Get, fields).ConfigureAwait(false);
        Console.WriteLine(result);
    }
}

