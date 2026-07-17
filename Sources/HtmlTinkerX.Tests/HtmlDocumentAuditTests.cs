namespace HtmlTinkerX.Tests;

public class HtmlDocumentAuditTests {
    [Fact]
    public void Analyze_UnsafeAndInaccessibleMarkup_ReturnsStructuredIssues() {
        string html = """
<!doctype html>
<html>
<head><title>Audit</title></head>
<body>
<main>
<h1>Audit</h1><h3>Skipped</h3>
<img src="image.png">
<button id="duplicate"></button>
<a id="duplicate" href="javascript:alert(1)"></a>
<input id="unlabelled">
</main>
</body>
</html>
""";

        HtmlDocumentAuditResult result = HtmlDocumentAudit.Analyze(html);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "document-language-missing");
        Assert.Contains(result.Issues, issue => issue.Code == "duplicate-id");
        Assert.Contains(result.Issues, issue => issue.Code == "image-alt-missing");
        Assert.Equal(2, result.Issues.Count(issue => issue.Code == "interactive-name-missing"));
        Assert.Contains(result.Issues, issue => issue.Code == "form-label-missing");
        Assert.Contains(result.Issues, issue => issue.Code == "unsafe-url-scheme");
        Assert.Contains(result.Issues, issue => issue.Code == "heading-level-skipped");
    }

    [Fact]
    public void Analyze_AccessibleDocument_IsValid() {
        string html = """
<!doctype html>
<html lang="en">
<head><title>Audit</title></head>
<body>
<main>
<h1>Audit</h1><h2>Details</h2>
<img src="decorative.png" alt="">
<button aria-label="Refresh"></button>
<a href="/docs">Documentation</a>
<a href="/home"><img src="home.png" alt="Home"></a>
<label for="query">Query</label><input id="query">
<input type="image" src="submit.png" alt="Submit">
<input type="submit"><input type="reset">
</main>
</body>
</html>
""";

        HtmlDocumentAuditResult result = HtmlDocumentAudit.Analyze(html);

        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Analyze_UnsupportedAbsoluteSchemes_AgreeWithGeneratedHtmlPolicy() {
        string html = """
<!doctype html>
<html lang="en">
<head><title>Audit</title></head>
<body>
<a href="data:image/svg+xml,&lt;svg onload='alert(1)'&gt;">Unsafe navigation</a>
<img src="data:image/png;base64,AAAA" alt="Safe embedded asset">
<link rel="icon" href="data:image/png;base64,AAAA">
<object data="data:text/html,&lt;script&gt;alert(1)&lt;/script&gt;"></object>
<form action="mailto:ops@example.com"><button>Send</button></form>
</body>
</html>
""";

        HtmlDocumentAuditResult result = HtmlDocumentAudit.Analyze(html);

        Assert.Equal(2, result.Issues.Count(issue => issue.Code == "unsafe-url-scheme"));
    }

    [Fact]
    public void Analyze_SelectAndTextareaContent_DoesNotReplaceAFormLabel() {
        string html = """
        <!doctype html>
        <html lang="en">
        <head><title>Audit</title></head>
        <body>
        <select><option>Visible option</option></select>
        <textarea>Default value</textarea>
        <span id="query-label">Query</span><input aria-labelledby="query-label">
        </body>
        </html>
        """;

        HtmlDocumentAuditResult result = HtmlDocumentAudit.Analyze(html);

        Assert.Equal(2, result.Issues.Count(issue => issue.Code == "form-label-missing"));
    }
}
