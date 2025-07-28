using System;
using System.Threading.Tasks;

namespace HtmlTinkerX.Examples;

/// <summary>
/// Demonstrates converting a relative path to a full path using <see cref="HtmlUtilities.ToFullPath(string)"/>.
/// </summary>
public static class ToFullPathExample {
    /// <summary>Executes the example logic.</summary>
    public static Task RunAsync() {
        string relative = ".";
        string full = relative.ToFullPath();
        Console.WriteLine(full);
        return Task.CompletedTask;
    }
}

