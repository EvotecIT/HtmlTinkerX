using System;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace HtmlTinkerX;

internal static class PlaywrightExtensions {
    public static Task DisposeAsync(this IPlaywright playwright) {
        if (playwright == null) {
            return Task.CompletedTask;
        }

        if (playwright is IAsyncDisposable asyncDisposable) {
            return asyncDisposable.DisposeAsync().AsTask();
        }

        playwright.Dispose();
        return Task.CompletedTask;
    }
}
