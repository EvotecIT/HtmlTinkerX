using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace HtmlTinkerX.Examples;

/// <summary>
/// Demonstrates a complete browser extraction workflow using local route stubs.
/// </summary>
public static class BrowserExtractionModeExample {
    private const string StoryUrl = "https://psparsehtml.local/local-extraction.html";

    private const string StaticHtml = """
<!doctype html>
<html>
<head>
<title>Browser extraction local story</title>
<script>
window.__APP_CONFIG__ = { apiBase: "/api", feature: "browser-extraction" };
document.addEventListener("DOMContentLoaded", () => {
  const root = document.getElementById("root");
  root.innerHTML = `
<button id="cookieBanner" onclick="this.remove()">Accept</button>
<main>
<h1>Search demo</h1>
<form id="searchForm">
<input id="search" name="q" type="search" autocomplete="off">
<button type="submit">Search</button>
</form>
<button id="loadMore" type="button">Load more</button>
<section id="results">No results yet.</section>
</main>`;
  localStorage.setItem("storyLocal", "1");
  sessionStorage.setItem("storySession", "1");
  document.getElementById("searchForm").addEventListener("submit", async (event) => {
    event.preventDefault();
    const query = document.getElementById("search").value;
    const response = await fetch("/api/products?q=" + encodeURIComponent(query));
    const data = await response.json();
    document.getElementById("results").innerHTML = data.items
      .map(item => `<article class="product"><h2>${item.name}</h2><p>${item.description}</p></article>`)
      .join("");
  });
  document.getElementById("loadMore").addEventListener("click", () => {
    document.getElementById("results").insertAdjacentHTML(
      "beforeend",
      '<article class="product"><h2>Workbench profile sample</h2><p>Added after a click.</p></article>');
  });
});
</script>
</head>
<body>
<div id="root">Loading...</div>
</body>
</html>
""";

    /// <summary>Runs the local browser extraction workflow and writes a short summary.</summary>
    public static async Task RunAsync() {
        HtmlExtractionPlan plan = HtmlExtractionPlanner.Analyze(StaticHtml);
        HtmlExtractionProfile profile = HtmlExtractionProfiles.Recommend(plan);

        string statePath = Path.Combine(Path.GetTempPath(), "htmltinkerx-browser-extraction-state.json");
        await using HtmlBrowserSession session = await HtmlBrowser.OpenSessionAsync("about:blank").ConfigureAwait(false);
        await HtmlBrowser.RegisterRouteAsync(session, "**/local-extraction.html", route =>
            route.FulfillAsync(new RouteFulfillOptions {
                Status = 200,
                ContentType = "text/html",
                Body = StaticHtml
            })).ConfigureAwait(false);
        await HtmlBrowser.RegisterRouteAsync(session, "**/api/products**", route =>
            route.FulfillAsync(new RouteFulfillOptions {
                Status = 200,
                ContentType = "application/json",
                Body = "{\"items\":[{\"name\":\"Found HtmlTinkerX guide\",\"description\":\"Rendered from a local API call.\"}],\"token\":\"local-secret-value\"}"
            })).ConfigureAwait(false);

        await HtmlBrowser.NavigateAsync(session, StoryUrl).ConfigureAwait(false);
        await HtmlBrowser.DismissCommonOverlaysAsync(session).ConfigureAwait(false);
        await HtmlBrowser.TypeInputAsync(session, "#search", "HtmlTinkerX", delayMs: 0).ConfigureAwait(false);
        await HtmlBrowser.PressKeysAsync(session, "#search", "Enter").ConfigureAwait(false);
        await HtmlBrowser.WaitForTextAsync(session, "Found HtmlTinkerX guide", "#results", exact: true).ConfigureAwait(false);
        await HtmlBrowser.ClickTextAsync(session, "Load more", exact: true).ConfigureAwait(false);
        await HtmlBrowser.WaitForTextAsync(session, "Workbench profile sample", "#results", exact: true).ConfigureAwait(false);
        await HtmlBrowser.WaitForElementStateAsync(session, "#results", visible: true, inViewport: true).ConfigureAwait(false);
        await HtmlBrowser.WaitUntilStableAsync(session, stableMilliseconds: 100, pollMilliseconds: 25).ConfigureAwait(false);

        IReadOnlyList<HtmlBrowserElementInfo> resultElements = await HtmlBrowser.GetElementsAsync(session, ".product", visibleOnly: true, includeAttributes: true).ConfigureAwait(false);
        bool resultsVisible = await HtmlBrowser.TestElementAsync(session, "#results", visible: true, inViewport: true).ConfigureAwait(false);
        await HtmlBrowser.ClickSelectorAsync(session, "#search").ConfigureAwait(false);
        HtmlBrowserElementInfo? activeElement = await HtmlBrowser.GetActiveElementAsync(session, includeAttributes: true).ConfigureAwait(false);
        await HtmlBrowser.SetStorageAsync(session, "Local", "storyMode", "browser-extraction").ConfigureAwait(false);
        IReadOnlyList<HtmlBrowserStorageItem> storage = await HtmlBrowser.GetStorageAsync(session, "All").ConfigureAwait(false);
        HtmlBrowserDiagnostics diagnostics = await HtmlBrowser.GetDiagnosticsAsync(session).ConfigureAwait(false);
        HtmlRenderedPageSnapshot snapshot = await HtmlBrowser.CreateSnapshotAsync(
            session,
            StoryUrl,
            includeStaticRenderedComparison: true,
            staticHtml: StaticHtml).ConfigureAwait(false);
        HtmlPageWorkbenchResult workbench = await HtmlPageWorkbench.AnalyzeAsync(
            StaticHtml,
            new HtmlPageWorkbenchOptions {
                BaseUri = new Uri(StoryUrl),
                RenderedSnapshot = snapshot
            }).ConfigureAwait(false);
        string contentPath = Path.Combine(Path.GetTempPath(), "htmltinkerx-browser-extraction-results.html");
        await HtmlBrowser.SaveContentAsync(session, contentPath, "#results").ConfigureAwait(false);
        await HtmlBrowser.ExportBrowserStateAsync(session, statePath).ConfigureAwait(false);

        Console.WriteLine($"Planner: {plan.RecommendedMode} / {profile.Name} / {profile.RenderProfile}");
        Console.WriteLine($"Workbench: {workbench.AnalysisMode} / {workbench.Title}");
        Console.WriteLine($"Visible result elements: {resultElements.Count}");
        Console.WriteLine($"Results visible: {resultsVisible}");
        Console.WriteLine($"Active element: {activeElement?.Id}");
        Console.WriteLine($"Storage entries: {storage.Count}");
        Console.WriteLine($"Observed API calls: {diagnostics.ObservedApiCalls.Count}");
        Console.WriteLine($"Rendered result present: {snapshot.Text.Contains("Found HtmlTinkerX guide", StringComparison.Ordinal)}");
        Console.WriteLine($"Saved content: {contentPath}");
        Console.WriteLine($"State path: {statePath}");
    }
}
