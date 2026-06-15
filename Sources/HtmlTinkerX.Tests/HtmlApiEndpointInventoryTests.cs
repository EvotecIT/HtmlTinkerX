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
        Assert.Equal(HtmlApiEndpointRiskLevel.Medium, readEndpoint.RiskLevel);
        Assert.Contains("unknown-method", readEndpoint.ReasonCodes);
        Assert.DoesNotContain("same-origin-read", readEndpoint.ReasonCodes);

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
    public void Build_RedactsEndpointDerivedNames() {
        HtmlPageWorkbenchResult workbench = new() {
            SourceUrl = "https://example.org/page",
            FinalUrl = "https://example.org/page",
            InteractionSurface = new[] {
                new HtmlInteractionSurfaceItem {
                    Kind = "Endpoint",
                    Name = "/api/session?token=abc123",
                    Url = "/api/session?token=abc123",
                    Source = "InlineScript"
                }
            }
        };

        HtmlApiEndpointRecord endpoint = Assert.Single(HtmlApiEndpointInventory.Build(workbench));

        Assert.Contains("token=<redacted>", endpoint.Name);
        Assert.DoesNotContain("abc123", endpoint.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_RedactsSensitiveMetadata() {
        HtmlPageWorkbenchResult workbench = new() {
            SourceUrl = "https://example.org/page",
            FinalUrl = "https://example.org/page",
            InteractionSurface = new[] {
                new HtmlInteractionSurfaceItem {
                    Kind = "LinkedEndpoint",
                    Name = "/api/items",
                    Url = "/api/items",
                    Source = "LinkedScript",
                    Metadata = "/app.js?token=abc123"
                }
            }
        };

        HtmlApiEndpointRecord endpoint = Assert.Single(HtmlApiEndpointInventory.Build(workbench));

        Assert.Contains("token=<redacted>", endpoint.Metadata);
        Assert.DoesNotContain("abc123", endpoint.Metadata, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_DoesNotTreatBenignNamesAsSensitiveQueryText() {
        HtmlPageWorkbenchResult workbench = new() {
            SourceUrl = "https://example.org/page",
            FinalUrl = "https://example.org/page",
            InteractionSurface = new[] {
                new HtmlInteractionSurfaceItem {
                    Kind = "Endpoint",
                    Name = "decodeKeyMetrics",
                    Url = "/graphql",
                    Method = "GET",
                    Source = "InlineScript",
                    Metadata = "decodeKeyMetrics"
                }
            }
        };

        HtmlApiEndpointRecord endpoint = Assert.Single(HtmlApiEndpointInventory.Build(workbench));

        Assert.False(endpoint.HasSensitiveQuery);
        Assert.Equal(HtmlApiEndpointRiskLevel.Low, endpoint.RiskLevel);
        Assert.Contains("same-origin-read", endpoint.ReasonCodes);
        Assert.DoesNotContain("sensitive-query-name", endpoint.ReasonCodes);
    }

    [Fact]
    public void Build_RedactsSensitiveUrlFragments() {
        HtmlPageWorkbenchResult workbench = new() {
            SourceUrl = "https://example.org/page",
            FinalUrl = "https://example.org/page",
            InteractionSurface = new[] {
                new HtmlInteractionSurfaceItem {
                    Kind = "Endpoint",
                    Name = "/callback#access_token=abc123",
                    Url = "/callback?ok=1#access_token=abc123",
                    Method = "GET",
                    Source = "InlineScript"
                }
            }
        };

        HtmlApiEndpointRecord endpoint = Assert.Single(HtmlApiEndpointInventory.Build(workbench));

        Assert.True(endpoint.HasSensitiveQuery);
        Assert.Equal(HtmlApiEndpointRiskLevel.High, endpoint.RiskLevel);
        Assert.Contains("access_token=<redacted>", endpoint.Url);
        Assert.Contains("access_token=<redacted>", endpoint.ResolvedUrl);
        Assert.Contains("access_token=<redacted>", endpoint.Name);
        Assert.DoesNotContain("abc123", endpoint.Url, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123", endpoint.ResolvedUrl, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123", endpoint.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_RedactsUrlUserInfo() {
        HtmlPageWorkbenchResult workbench = new() {
            SourceUrl = "https://example.org/page",
            FinalUrl = "https://example.org/page",
            InteractionSurface = new[] {
                new HtmlInteractionSurfaceItem {
                    Kind = "Endpoint",
                    Name = "https://user:pass@api.example.net/items",
                    Url = "https://user:pass@api.example.net/items",
                    Method = "GET",
                    Source = "InlineScript"
                }
            }
        };

        HtmlApiEndpointRecord endpoint = Assert.Single(HtmlApiEndpointInventory.Build(workbench));

        Assert.True(endpoint.HasSensitiveQuery);
        Assert.Contains("<redacted>@api.example.net", endpoint.Url);
        Assert.Contains("<redacted>@api.example.net", endpoint.ResolvedUrl);
        Assert.Contains("<redacted>@api.example.net", endpoint.Name);
        Assert.Contains("<redacted>@api.example.net", endpoint.Origin);
        Assert.DoesNotContain("user:pass", endpoint.Url, StringComparison.Ordinal);
        Assert.DoesNotContain("user:pass", endpoint.ResolvedUrl, StringComparison.Ordinal);
        Assert.DoesNotContain("user:pass", endpoint.Name, StringComparison.Ordinal);
        Assert.DoesNotContain("user:pass", endpoint.Origin, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_RedactsUserInfoInEndpointMetadata() {
        HtmlPageWorkbenchResult workbench = new() {
            SourceUrl = "https://example.org/page",
            FinalUrl = "https://example.org/page",
            InteractionSurface = new[] {
                new HtmlInteractionSurfaceItem {
                    Kind = "LinkedEndpoint",
                    Name = "/api/items",
                    Url = "/api/items",
                    Method = "GET",
                    Source = "LinkedScript",
                    Metadata = "https://user:pass@cdn.example.net/app.js"
                }
            }
        };

        HtmlApiEndpointRecord endpoint = Assert.Single(HtmlApiEndpointInventory.Build(workbench));

        Assert.True(endpoint.HasSensitiveQuery);
        Assert.Contains("<redacted>@cdn.example.net", endpoint.Metadata);
        Assert.DoesNotContain("user:pass", endpoint.Metadata, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_RedactsNestedSensitiveUrlParameters() {
        HtmlPageWorkbenchResult workbench = new() {
            SourceUrl = "https://example.org/page",
            FinalUrl = "https://example.org/page",
            InteractionSurface = new[] {
                new HtmlInteractionSurfaceItem {
                    Kind = "Endpoint",
                    Name = "/api/redirect?redirect=%2Fcallback%3Faccess_token%3Dabc123",
                    Url = "/api/redirect?redirect=%2Fcallback%3Faccess_token%3Dabc123",
                    Method = "GET",
                    Source = "InlineScript"
                }
            }
        };

        HtmlApiEndpointRecord endpoint = Assert.Single(HtmlApiEndpointInventory.Build(workbench));

        Assert.True(endpoint.HasSensitiveQuery);
        Assert.DoesNotContain("abc123", endpoint.Url, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123", endpoint.ResolvedUrl, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123", endpoint.Name, StringComparison.Ordinal);
        Assert.Contains(Uri.EscapeDataString("access_token=<redacted>"), endpoint.Url);
        Assert.Contains(Uri.EscapeDataString("access_token=<redacted>"), endpoint.ResolvedUrl);
        Assert.Contains(Uri.EscapeDataString("access_token=<redacted>"), endpoint.Name);
    }

    [Fact]
    public void Build_RedactsProtocolRelativeUserInfo() {
        HtmlPageWorkbenchResult workbench = new() {
            SourceUrl = "https://example.org/page",
            FinalUrl = "https://example.org/page",
            InteractionSurface = new[] {
                new HtmlInteractionSurfaceItem {
                    Kind = "Endpoint",
                    Name = "//user:pass@api.example.net/items",
                    Url = "//user:pass@api.example.net/items",
                    Method = "GET",
                    Source = "InlineScript"
                }
            }
        };

        HtmlApiEndpointRecord endpoint = Assert.Single(HtmlApiEndpointInventory.Build(workbench));

        Assert.True(endpoint.HasSensitiveQuery);
        Assert.Contains("//<redacted>@api.example.net", endpoint.Url);
        Assert.Contains("<redacted>@api.example.net", endpoint.ResolvedUrl);
        Assert.Contains("//<redacted>@api.example.net", endpoint.Name);
        Assert.DoesNotContain("user:pass", endpoint.Url, StringComparison.Ordinal);
        Assert.DoesNotContain("user:pass", endpoint.ResolvedUrl, StringComparison.Ordinal);
        Assert.DoesNotContain("user:pass", endpoint.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_TreatsUnknownSameOriginMethodsAsReviewRisk() {
        HtmlPageWorkbenchResult workbench = new() {
            SourceUrl = "https://example.org/page",
            FinalUrl = "https://example.org/page",
            InteractionSurface = new[] {
                new HtmlInteractionSurfaceItem {
                    Kind = "Endpoint",
                    Name = "/api/ambiguous",
                    Url = "/api/ambiguous",
                    Source = "InlineScript"
                }
            }
        };

        HtmlApiEndpointRecord endpoint = Assert.Single(HtmlApiEndpointInventory.Build(workbench));

        Assert.Equal("UNKNOWN", endpoint.Method);
        Assert.Equal(HtmlApiEndpointRiskLevel.Medium, endpoint.RiskLevel);
        Assert.Contains("unknown-method", endpoint.ReasonCodes);
        Assert.DoesNotContain("same-origin-read", endpoint.ReasonCodes);
    }

    [Fact]
    public void Build_SkipsLinkedScriptDownloadDiagnosticsWithoutEndpointUrl() {
        HtmlPageWorkbenchResult workbench = new() {
            SourceUrl = "https://example.org/page",
            FinalUrl = "https://example.org/page",
            InteractionSurface = new[] {
                new HtmlInteractionSurfaceItem {
                    Kind = "LinkedEndpoint",
                    Name = "https://example.org/broken.js",
                    Url = string.Empty,
                    Source = "LinkedScript",
                    Metadata = "404 Not Found"
                }
            }
        };

        Assert.Empty(HtmlApiEndpointInventory.Build(workbench));
    }

    [Fact]
    public void Build_PreservesDistinctOperationsForSameEndpoint() {
        HtmlPageWorkbenchResult workbench = new() {
            SourceUrl = "https://example.org/page",
            FinalUrl = "https://example.org/page",
            InteractionSurface = new[] {
                new HtmlInteractionSurfaceItem {
                    Kind = "Endpoint",
                    Name = "GetUser",
                    Url = "/graphql",
                    Method = "POST",
                    Source = "InlineScript"
                },
                new HtmlInteractionSurfaceItem {
                    Kind = "Endpoint",
                    Name = "UpdateUser",
                    Url = "/graphql",
                    Method = "POST",
                    Source = "InlineScript"
                }
            }
        };

        IReadOnlyList<HtmlApiEndpointRecord> endpoints = HtmlApiEndpointInventory.Build(workbench);

        Assert.Equal(2, endpoints.Count);
        Assert.Contains(endpoints, endpoint => endpoint.Name == "GetUser");
        Assert.Contains(endpoints, endpoint => endpoint.Name == "UpdateUser");
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
