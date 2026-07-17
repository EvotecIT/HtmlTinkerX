using System;
using System.IO;

namespace HtmlTinkerX.Tests;

[Collection("Playwright collection")]
public class HtmlBrowserStyleInspectionTests {
    [Fact]
    public async Task StyleInspectionAndAudit_UseRenderedDocumentContract() {
        string file = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.html");
        File.WriteAllText(file, """
<!doctype html>
<html lang="en" style="--brand: #123456">
<head><title>Style inspection</title></head>
<body><main><h1>Style</h1><button id="action" aria-label="Run" style="color: rgb(1, 2, 3)"></button></main></body>
</html>
""");

        await using HtmlBrowserSession session = await HtmlBrowser.OpenSessionAsync(new Uri(file).AbsoluteUri);
        try {
            IReadOnlyDictionary<string, string> styles = await HtmlBrowser.GetComputedStylesAsync(session, "#action", new[] { "color" });
            IReadOnlyDictionary<string, string> variables = await HtmlBrowser.GetCssCustomPropertiesAsync(session, "html", new[] { "--brand" });
            HtmlDocumentAuditResult audit = await HtmlBrowser.AuditDocumentAsync(session);

            Assert.Equal("rgb(1, 2, 3)", styles["color"]);
            Assert.Equal("#123456", variables["--brand"]);
            Assert.True(audit.IsValid);
        } finally {
            await HtmlBrowser.CloseSessionAsync(session);
            File.Delete(file);
        }
    }
}
