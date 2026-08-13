namespace HtmlTinkerX;

using Microsoft.Playwright;
using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Owns browser processes that finish launching after their public session was cancelled.</summary>
public static partial class HtmlBrowser {
    private static readonly TimeSpan LateBrowserLaunchCleanupTimeout = TimeSpan.FromSeconds(2);

    private static void CloseBrowserWhenLaunchCompletes(Task<IBrowser> launch, IPlaywright playwright) =>
        StartBestEffortClose(async () => {
            try {
                Task deadline = Task.Delay(LateBrowserLaunchCleanupTimeout);
                if (await Task.WhenAny(launch, deadline).ConfigureAwait(false) == launch) {
                    IBrowser browser = await launch.ConfigureAwait(false);
                    Task close = browser.CloseAsync();
                    if (await Task.WhenAny(close, deadline).ConfigureAwait(false) == close) {
                        await close.ConfigureAwait(false);
                    } else {
                        ObserveLateFault(close);
                    }
                } else {
                    ObserveLateFault(launch);
                }
            } finally {
                playwright.Dispose();
            }
        });

    private static void ClosePersistentContextWhenLaunchCompletes(
        Task<IBrowserContext> launch,
        IPlaywright playwright) =>
        StartBestEffortClose(async () => {
            try {
                Task deadline = Task.Delay(LateBrowserLaunchCleanupTimeout);
                if (await Task.WhenAny(launch, deadline).ConfigureAwait(false) == launch) {
                    IBrowserContext context = await launch.ConfigureAwait(false);
                    Task close = context.CloseAsync();
                    if (await Task.WhenAny(close, deadline).ConfigureAwait(false) == close) {
                        await close.ConfigureAwait(false);
                    } else {
                        ObserveLateFault(close);
                    }
                } else {
                    ObserveLateFault(launch);
                }
            } finally {
                playwright.Dispose();
            }
        });

    private static void DisposePlaywrightWhenCreated(Task<IPlaywright> creation) =>
        _ = creation.ContinueWith(
            static completed => {
                if (completed.Status == TaskStatus.RanToCompletion) completed.Result.Dispose();
                else _ = completed.Exception;
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
}
