using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

[Collection("Playwright collection")]
public class HtmlBrowserElementInspectionTests {
    [Fact]
    public async Task BrowserInspectionStorageAndSave_HavePairedCoreStory() {
        string file = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.html");
        string output = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.html");
        File.WriteAllText(file, """
<!doctype html>
<html>
<body>
<main>
<input id="search" name="q" value="">
<button class="item" data-kind="primary">First</button>
<button class="item" data-kind="secondary">Second</button>
<input id="flag" type="checkbox" checked>
<section id="target">Ready</section>
</main>
<script>
localStorage.setItem('existingLocal', '1');
sessionStorage.setItem('existingSession', '2');
</script>
</body>
</html>
""");

        await using HtmlBrowserSession session = await HtmlBrowser.OpenSessionAsync(new Uri(file).AbsoluteUri);
        try {
            var elements = await HtmlBrowser.GetElementsAsync(session, ".item", includeAttributes: true, includeHtml: true);

            Assert.Equal(2, elements.Count);
            Assert.All(elements, element => Assert.True(element.Visible));
            Assert.Equal("button", elements[0].Tag);
            Assert.Equal("primary", elements[0].Attributes["data-kind"]);
            Assert.Contains("First", elements[0].OuterHtml);

            Assert.True(await HtmlBrowser.TestElementAsync(session, "#flag", checkedState: true));
            Assert.True(await HtmlBrowser.TestElementAsync(session, "#target", visible: true, inViewport: true));

            await HtmlBrowser.ClickSelectorAsync(session, "#search");
            HtmlBrowserElementInfo? active = await HtmlBrowser.GetActiveElementAsync(session, includeAttributes: true);
            Assert.NotNull(active);
            Assert.Equal("search", active!.Id);
            Assert.Equal("q", active.Attributes["name"]);

            await HtmlBrowser.SetStorageAsync(session, "Local", "story", "enabled");
            var storage = await HtmlBrowser.GetStorageAsync(session, "All");
            Assert.Contains(storage, item => item.Scope == "Local" && item.Key == "story" && item.Value == "enabled");
            Assert.Contains(storage, item => item.Scope == "Session" && item.Key == "existingSession" && item.Value == "2");

            await HtmlBrowser.SaveContentAsync(session, output, "#target");
            Assert.Contains("Ready", File.ReadAllText(output));
        } finally {
            await HtmlBrowser.CloseSessionAsync(session);
            File.Delete(file);
            if (File.Exists(output)) {
                File.Delete(output);
            }
        }
    }

    [Fact]
    public async Task GetElementsAsync_ReportsControlsDisabledByFieldsetAsDisabled() {
        string file = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.html");
        File.WriteAllText(file, """
<!doctype html>
<html>
<body>
<fieldset disabled>
<input id="field" name="field" value="value">
</fieldset>
</body>
</html>
""");

        await using HtmlBrowserSession session = await HtmlBrowser.OpenSessionAsync(new Uri(file).AbsoluteUri);
        try {
            HtmlBrowserElementInfo element = Assert.Single(await HtmlBrowser.GetElementsAsync(session, "#field"));

            Assert.False(element.Enabled);
            Assert.False(element.Editable);
        } finally {
            await HtmlBrowser.CloseSessionAsync(session);
            File.Delete(file);
        }
    }

    [Fact]
    public async Task GetDiagnosticsAsync_ReturnsStorageWarningsForOpaqueOrigins() {
        await using HtmlBrowserSession session = await HtmlBrowser.OpenSessionAsync("data:text/html,<html><body>opaque</body></html>");
        try {
            HtmlBrowserDiagnostics diagnostics = await HtmlBrowser.GetDiagnosticsAsync(session);

            Assert.Empty(diagnostics.LocalStorageKeys);
            Assert.Empty(diagnostics.SessionStorageKeys);
            Assert.Contains(diagnostics.ConsistencyWarnings, warning => warning.Contains("localStorage access was denied", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(diagnostics.ConsistencyWarnings, warning => warning.Contains("sessionStorage access was denied", StringComparison.OrdinalIgnoreCase));
        } finally {
            await HtmlBrowser.CloseSessionAsync(session);
        }
    }
}
