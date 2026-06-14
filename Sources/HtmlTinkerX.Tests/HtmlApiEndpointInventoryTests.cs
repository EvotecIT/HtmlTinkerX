using System;
using System.Linq;
using System.Threading.Tasks;

namespace HtmlTinkerX.Tests;

public class HtmlApiEndpointInventoryTests {
    [Fact]
    public async Task Build_ClassifiesEndpointsAndAvoidsSensitiveQueryValues() {
        string html = """
<html>
<head>
<script>
fetch("/api/items");
fetch("https://api.example.net/public");
fetch("/api/session?token=abc123");
</script>
</head>
<body>
<form method="post" action="/submit"><input name="name"></form>
</body>
</html>
""";

        HtmlPageWorkbenchResult workbench = await HtmlPageWorkbench.AnalyzeAsync(
            html,
            new HtmlPageWorkbenchOptions {
                BaseUri = new Uri("https://example.org/page")
            });

        HtmlApiEndpointRecord readEndpoint = Assert.Single(workbench.ApiEndpoints, record => record.ResolvedUrl == "https://example.org/api/items");
        Assert.Equal(HtmlApiEndpointRiskLevel.Low, readEndpoint.RiskLevel);
        Assert.Contains("same-origin-read", readEndpoint.ReasonCodes);

        HtmlApiEndpointRecord externalEndpoint = Assert.Single(workbench.ApiEndpoints, record => record.ResolvedUrl == "https://api.example.net/public");
        Assert.Equal(HtmlApiEndpointRiskLevel.Medium, externalEndpoint.RiskLevel);
        Assert.True(externalEndpoint.IsExternal);
        Assert.Contains("external-origin", externalEndpoint.ReasonCodes);

        HtmlApiEndpointRecord formEndpoint = Assert.Single(workbench.ApiEndpoints, record => record.Kind == "Form");
        Assert.Equal("POST", formEndpoint.Method);
        Assert.Equal(HtmlApiEndpointRiskLevel.High, formEndpoint.RiskLevel);
        Assert.True(formEndpoint.IsStateChanging);
        Assert.Contains("form-action", formEndpoint.ReasonCodes);
        Assert.Contains("state-changing-method", formEndpoint.ReasonCodes);

        HtmlApiEndpointRecord sensitiveEndpoint = Assert.Single(workbench.ApiEndpoints, record => record.ResolvedUrl.StartsWith("https://example.org/api/session", StringComparison.Ordinal));
        Assert.Equal(HtmlApiEndpointRiskLevel.High, sensitiveEndpoint.RiskLevel);
        Assert.True(sensitiveEndpoint.HasSensitiveQuery);
        Assert.Contains("sensitive-query-name", sensitiveEndpoint.ReasonCodes);
        Assert.Contains("token=<redacted>", sensitiveEndpoint.Url);
        Assert.Contains("token=<redacted>", sensitiveEndpoint.ResolvedUrl);
        Assert.DoesNotContain("abc123", sensitiveEndpoint.Url, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123", sensitiveEndpoint.ResolvedUrl, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123", sensitiveEndpoint.Name, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123", sensitiveEndpoint.Metadata, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Build_CanFilterFormsOrScriptEndpoints() {
        string html = """
<html>
<head><script>fetch("/api/items");</script></head>
<body><form method="post" action="/submit"><input name="name"></form></body>
</html>
""";

        HtmlPageWorkbenchResult workbench = await HtmlPageWorkbench.AnalyzeAsync(
            html,
            new HtmlPageWorkbenchOptions {
                BaseUri = new Uri("https://example.org/page")
            });

        var withoutForms = HtmlApiEndpointInventory.Build(
            workbench,
            new HtmlApiEndpointInventoryOptions {
                IncludeForms = false
            });
        Assert.DoesNotContain(withoutForms, record => record.Kind == "Form");
        Assert.Contains(withoutForms, record => record.Kind == "Endpoint");

        var withoutScriptEndpoints = HtmlApiEndpointInventory.Build(
            workbench,
            new HtmlApiEndpointInventoryOptions {
                IncludeScriptEndpoints = false
            });
        Assert.Contains(withoutScriptEndpoints, record => record.Kind == "Form");
        Assert.DoesNotContain(withoutScriptEndpoints, record => record.Kind == "Endpoint");
    }
}
