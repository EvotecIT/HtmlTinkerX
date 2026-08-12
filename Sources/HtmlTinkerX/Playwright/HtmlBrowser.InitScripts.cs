using Microsoft.Playwright;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

public static partial class HtmlBrowser {
    private static async Task ApplyInitScriptsAsync(IBrowserContext context, HtmlBrowserLaunchOptions options, CancellationToken cancellationToken) {
        if (options.PreventSsoAutoSubmit) {
            cancellationToken.ThrowIfCancellationRequested();
            await context.AddInitScriptAsync(PreventSsoAutoSubmitInitScript).ConfigureAwait(false);
        }

        foreach (string script in options.InitScripts) {
            cancellationToken.ThrowIfCancellationRequested();
            await context.AddInitScriptAsync(script).ConfigureAwait(false);
        }

        foreach (string scriptPath in options.InitScriptPaths) {
            cancellationToken.ThrowIfCancellationRequested();
            await context.AddInitScriptAsync(scriptPath: scriptPath.ToFullPath()).ConfigureAwait(false);
        }
    }

    private static async Task ApplyInitScriptsAsync(IPage page, HtmlBrowserLaunchOptions options, CancellationToken cancellationToken) {
        if (options.PreventSsoAutoSubmit) {
            cancellationToken.ThrowIfCancellationRequested();
            await page.AddInitScriptAsync(PreventSsoAutoSubmitInitScript).ConfigureAwait(false);
        }

        foreach (string script in options.InitScripts) {
            cancellationToken.ThrowIfCancellationRequested();
            await page.AddInitScriptAsync(script).ConfigureAwait(false);
        }

        foreach (string scriptPath in options.InitScriptPaths) {
            cancellationToken.ThrowIfCancellationRequested();
            await page.AddInitScriptAsync(scriptPath: scriptPath.ToFullPath()).ConfigureAwait(false);
        }
    }
}
