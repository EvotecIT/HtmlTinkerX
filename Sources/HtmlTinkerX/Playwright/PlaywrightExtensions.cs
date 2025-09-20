using Microsoft.Playwright;
using System;
using System.Threading.Tasks;

namespace HtmlTinkerX;

internal static class PlaywrightExtensions {
    public static ValueTask DisposeAsync(this IPlaywright playwright) {
        if (playwright is IAsyncDisposable asyncDisposable) {
            return asyncDisposable.DisposeAsync();
        }

        playwright.Dispose();
        return new ValueTask(Task.CompletedTask);
    }
}
