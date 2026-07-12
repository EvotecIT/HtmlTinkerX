using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public partial class HtmlCrawlerStructuredJsonTests {

    [Fact]
    public async Task CrawlAsync_GetParameterTables_PreserveQueryLocationsCompoundNamesAndPublicAuth() {
        Dictionary<string, string> responses = new() {
            ["/docs/search"] = """
            <html>
              <body>
                <main>
                  <h1>Search API</h1>
                  <h2>GET /v1/search</h2>
                  <p>No API key required.</p>
                  <h3>Parameters</h3>
                  <table class="parameters">
                    <tr><th>Parameter Name</th><th>Location</th><th>Type</th><th>Required</th><th>Description</th></tr>
                    <tr><td>search</td><td></td><td>string</td><td>No</td><td>Search phrase.</td></tr>
                    <tr><td>limit</td><td>Query parameter</td><td>integer</td><td>No</td><td>Maximum results.</td></tr>
                  </table>
                  <pre><code class="language-http">GET /v1/search?search=alpha&amp;limit=10 HTTP/1.1
                  Accept: application/json</code></pre>
                  <h3>Response 200</h3>
                  <pre><code class="language-json">[{ "id": "item-1" }]</code></pre>
                </main>
              </body>
            </html>
            """
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl + "docs/search", new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Selector = "main",
                IncludeStructuredJson = true
            });

            HtmlCrawlStructuredApiEndpoint endpoint = Assert.Single(Assert.Single(result.Pages).StructuredJson!.ApiEndpoints);
            Assert.Equal(2, endpoint.Parameters.Count);
            Assert.Equal(2, endpoint.QueryParameters.Count);
            Assert.Empty(endpoint.BodyParameters);
            Assert.Contains(endpoint.QueryParameters, parameter => string.Equals(parameter.Name, "search", StringComparison.Ordinal));
            Assert.Contains(endpoint.QueryParameters, parameter => string.Equals(parameter.Name, "limit", StringComparison.Ordinal));
            Assert.True(
                endpoint.Authentication.Headers.Count == 0,
                $"Unexpected auth: required={endpoint.Authentication.Required}; headers={string.Join(",", endpoint.Authentication.Headers)}; schemes={string.Join(",", endpoint.Authentication.Schemes)}; summary={endpoint.Authentication.Summary}");
            Assert.Empty(endpoint.Authentication.Schemes);
            Assert.False(endpoint.Authentication.Required);

            IDictionary<string, object?> paths = Assert.IsAssignableFrom<IDictionary<string, object?>>(result.OpenApiDocument["paths"]);
            IDictionary<string, object?> searchPath = Assert.IsAssignableFrom<IDictionary<string, object?>>(paths["/v1/search"]);
            IDictionary<string, object?> getOperation = Assert.IsAssignableFrom<IDictionary<string, object?>>(searchPath["get"]);
            List<object> strictParameters = Assert.IsAssignableFrom<List<object>>(getOperation["parameters"]);
            Assert.Equal(2, strictParameters.Count);
            Assert.DoesNotContain("requestBody", getOperation.Keys);
            Assert.DoesNotContain("security", getOperation.Keys);
        } finally {
            DisposeListenerSafely(server);
        }
    }

    [Fact]
    public async Task CrawlAsync_IncludeStructuredJson_KeepsWeakEndpointsInOpenApiLikeButSkipsStrictPromotion() {
        Dictionary<string, string> responses = new() {
            ["/docs/mystery-widget"] = """
            <html>
              <head>
                <title>Mystery Widget API</title>
              </head>
              <body>
                <main>
                  <h1>Mystery Widget API</h1>
                  <h2>POST /v1/mystery-widgets</h2>
                  <p>Create a mystery widget.</p>
                </main>
              </body>
            </html>
            """
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        string outputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl + "docs/mystery-widget", new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Selector = "main",
                IncludeStructuredJson = true,
                OutputPath = outputPath
            });

            Assert.NotNull(result.OpenApiLike);
            Assert.True(result.OpenApiLike.Paths.ContainsKey("/v1/mystery-widgets"));
            HtmlCrawlStructuredOpenApiOperation operation = result.OpenApiLike.Paths["/v1/mystery-widgets"].Operations["post"];
            Assert.False(operation.StrictOpenApiEligible);
            Assert.True(operation.StrictOpenApiWarnings.Count > 0);
            Assert.Contains(operation.StrictOpenApiWarnings, warning => string.Equals(warning, "missing success response contract", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(rootUrl + "docs/mystery-widget", operation.Provenance.PageUrls, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Heading", operation.Provenance.SourceKinds, StringComparer.OrdinalIgnoreCase);
            Assert.NotEmpty(operation.Provenance.Entries);
            Assert.Equal(0, result.OpenApiLike.StrictOpenApiEligibleOperationCount);
            Assert.Equal(1, result.OpenApiLike.StrictOpenApiSkippedOperationCount);

            IDictionary<string, object?> strictPaths = Assert.IsAssignableFrom<IDictionary<string, object?>>(result.OpenApiDocument["paths"]);
            Assert.Empty(strictPaths);
            Assert.False(string.IsNullOrWhiteSpace(result.OpenApiPath));
            string openApiJson = File.ReadAllText(result.OpenApiPath!);
            Assert.Contains("\"paths\": {}", openApiJson, StringComparison.Ordinal);
            Assert.Contains("\"skippedOperationCount\": 1", openApiJson, StringComparison.Ordinal);
        } finally {
            DisposeListenerSafely(server);
            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CrawlAsync_IncludeStructuredJson_StrictOpenApiInfersPathParametersAndOauth2Schemes() {
        Dictionary<string, string> responses = new() {
            ["/docs/widgets/get-widget"] = """
            <html>
              <head>
                <title>Get widget</title>
              </head>
              <body>
                <main>
                  <h1>Get widget</h1>
                  <h2>GET /v1/widgets/{id}</h2>
                  <p>Use OAuth2 access tokens to call this endpoint.</p>
                  <h3>Parameters</h3>
                  <table class="parameters">
                    <tr><th>Name</th><th>Type</th><th>Required</th><th>Description</th></tr>
                    <tr><td>id</td><td>string</td><td>Yes</td><td>Widget identifier.</td></tr>
                  </table>
                  <h3>Response 200</h3>
                  <pre><code class="language-json">{ "id": "wid_123", "name": "Alpha" }</code></pre>
                </main>
              </body>
            </html>
            """
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl + "docs/widgets/get-widget", new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Selector = "main",
                IncludeStructuredJson = true
            });

            IDictionary<string, object?> paths = Assert.IsAssignableFrom<IDictionary<string, object?>>(result.OpenApiDocument["paths"]);
            IDictionary<string, object?> widgetsPath = Assert.IsAssignableFrom<IDictionary<string, object?>>(paths["/v1/widgets/{id}"]);
            IDictionary<string, object?> getOperation = Assert.IsAssignableFrom<IDictionary<string, object?>>(widgetsPath["get"]);
            Assert.False(getOperation.ContainsKey("requestBody"));
            List<object> parameters = Assert.IsAssignableFrom<List<object>>(getOperation["parameters"]);
            IDictionary<string, object?> idParameter = Assert.IsAssignableFrom<IDictionary<string, object?>>(Assert.Single(parameters));
            Assert.Equal("id", idParameter["name"] as string);
            Assert.Equal("path", idParameter["in"] as string);
            Assert.True((bool)idParameter["required"]!);

            IDictionary<string, object?> components = Assert.IsAssignableFrom<IDictionary<string, object?>>(result.OpenApiDocument["components"]);
            IDictionary<string, object?> securitySchemes = Assert.IsAssignableFrom<IDictionary<string, object?>>(components["securitySchemes"]);
            IDictionary<string, object?> securityScheme = Assert.IsAssignableFrom<IDictionary<string, object?>>(Assert.Single(securitySchemes).Value);
            Assert.Equal("oauth2", securityScheme["type"] as string);
            Assert.True(securityScheme.ContainsKey("flows"));
        } finally {
            DisposeListenerSafely(server);
        }
    }

    [Fact]
    public async Task CrawlAsync_IncludeStructuredJson_CurlSamplesPreferCommandTargetOverPayloadUrls() {
        Dictionary<string, string> responses = new() {
            ["/docs/webhooks/create"] = """
            <html>
              <head>
                <title>Create webhook</title>
              </head>
              <body>
                <main>
                  <h1>Create webhook</h1>
                  <pre><code class="language-bash">curl -X POST -H 'Referer: https://docs.example.com/reference' -H 'Content-Type: application/json' -d '{"callback":"https://hooks.example.com/incoming"}' https://api.example.com/v1/webhooks</code></pre>
                </main>
              </body>
            </html>
            """
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl + "docs/webhooks/create", new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Selector = "main",
                IncludeStructuredJson = true
            });

            HtmlCrawlStructuredJson structuredJson = Assert.Single(result.Pages).StructuredJson!;
            HtmlCrawlStructuredCodeSample sample = Assert.Single(structuredJson.CodeSamples);
            Assert.Equal("POST", sample.Method);
            Assert.Equal("/v1/webhooks", sample.Path);

            HtmlCrawlStructuredApiEndpoint endpoint = Assert.Single(structuredJson.ApiEndpoints);
            Assert.Equal("POST", endpoint.Method);
            Assert.Equal("/v1/webhooks", endpoint.Path);

            HtmlCrawlStructuredRequestExample requestExample = Assert.Single(endpoint.RequestExamples);
            Assert.Equal("POST", requestExample.Method);
            Assert.Equal("/v1/webhooks", requestExample.Path);
            Assert.Contains("https://hooks.example.com/incoming", requestExample.Body, StringComparison.Ordinal);
        } finally {
            DisposeListenerSafely(server);
        }
    }

    [Fact]
    public async Task CrawlAsync_IncludeStructuredJson_DetectsRootPathEndpoints() {
        Dictionary<string, string> responses = new() {
            ["/docs/root"] = """
            <html>
              <head>
                <title>Root endpoint</title>
              </head>
              <body>
                <main>
                  <h1>Root endpoint</h1>
                  <h2>GET /</h2>
                  <p>Returns API root metadata.</p>
                  <pre><code class="language-http">GET / HTTP/1.1
                  Host: api.example.com

                  </code></pre>
                  <h3>Response 200</h3>
                  <pre><code class="language-json">{ "name": "Example API" }</code></pre>
                </main>
              </body>
            </html>
            """
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl + "docs/root", new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Selector = "main",
                IncludeStructuredJson = true
            });

            HtmlCrawlStructuredJson structuredJson = Assert.Single(result.Pages).StructuredJson!;
            HtmlCrawlStructuredApiEndpoint endpoint = Assert.Single(structuredJson.ApiEndpoints);
            Assert.Equal("GET", endpoint.Method);
            Assert.Equal("/", endpoint.Path);
            HtmlCrawlStructuredRequestExample requestExample = Assert.Single(endpoint.RequestExamples);
            Assert.Equal("GET", requestExample.Method);
            Assert.Equal("/", requestExample.Path);
            Assert.True(string.IsNullOrWhiteSpace(requestExample.Body));

            Assert.True(structuredJson.OpenApiLike.Paths.ContainsKey("/"));
            Assert.True(result.OpenApiLike.Paths.ContainsKey("/"));

            IDictionary<string, object?> paths = Assert.IsAssignableFrom<IDictionary<string, object?>>(result.OpenApiDocument["paths"]);
            Assert.True(paths.ContainsKey("/"));
        } finally {
            DisposeListenerSafely(server);
        }
    }

    [Fact]
    public async Task CrawlAsync_IncludeStructuredJson_DoesNotTreatJsonResponsesUnderMethodHeadingsAsRequests() {
        Dictionary<string, string> responses = new() {
            ["/docs/users/list"] = """
            <html>
              <head>
                <title>List users</title>
              </head>
              <body>
                <main>
                  <h1>List users</h1>
                  <h2>GET /v1/users</h2>
                  <p>Returns all users.</p>
                  <pre><code class="language-json">[
                    { "id": "usr_123", "name": "Ada" }
                  ]</code></pre>
                </main>
              </body>
            </html>
            """
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl + "docs/users/list", new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Selector = "main",
                IncludeStructuredJson = true
            });

            HtmlCrawlStructuredJson structuredJson = Assert.Single(result.Pages).StructuredJson!;
            HtmlCrawlStructuredCodeSample sample = Assert.Single(structuredJson.CodeSamples);
            Assert.Null(sample.Method);
            Assert.Null(sample.Path);

            HtmlCrawlStructuredApiEndpoint endpoint = Assert.Single(structuredJson.ApiEndpoints);
            Assert.Equal("GET", endpoint.Method);
            Assert.Equal("/v1/users", endpoint.Path);
            Assert.Empty(endpoint.RequestExamples);
            HtmlCrawlStructuredResponseExample responseExample = Assert.Single(endpoint.ResponseExamples);
            Assert.False(responseExample.IsError);
            Assert.NotNull(responseExample.JsonBody);

            IDictionary<string, object?> paths = Assert.IsAssignableFrom<IDictionary<string, object?>>(result.OpenApiDocument["paths"]);
            IDictionary<string, object?> usersPath = Assert.IsAssignableFrom<IDictionary<string, object?>>(paths["/v1/users"]);
            IDictionary<string, object?> getOperation = Assert.IsAssignableFrom<IDictionary<string, object?>>(usersPath["get"]);
            Assert.False(getOperation.ContainsKey("requestBody"));
        } finally {
            DisposeListenerSafely(server);
        }
    }

    [Fact]
    public async Task CrawlAsync_IncludeStructuredJson_ClassifiesJsonRequestBodySamplesUnderRequestHeadingsAsRequests() {
        Dictionary<string, string> responses = new() {
            ["/docs/widgets/create"] = """
            <html>
              <head>
                <title>Create widget</title>
              </head>
              <body>
                <main>
                  <h1>Create widget</h1>
                  <h2>POST /v1/widgets</h2>
                  <h3>Request body</h3>
                  <pre><code class="language-json">{ "name": "Alpha" }</code></pre>
                  <h3>Response 201</h3>
                  <pre><code class="language-json">{ "id": "wid_123", "name": "Alpha" }</code></pre>
                </main>
              </body>
            </html>
            """
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl + "docs/widgets/create", new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Selector = "main",
                IncludeStructuredJson = true
            });

            HtmlCrawlStructuredJson structuredJson = Assert.Single(result.Pages).StructuredJson!;
            HtmlCrawlStructuredCodeSample requestSample = Assert.Single(structuredJson.CodeSamples,
                sample => string.Equals(sample.Heading, "Request body", StringComparison.OrdinalIgnoreCase));
            Assert.Equal("POST", requestSample.Method);
            Assert.Equal("/v1/widgets", requestSample.Path);

            HtmlCrawlStructuredApiEndpoint endpoint = Assert.Single(structuredJson.ApiEndpoints);
            HtmlCrawlStructuredRequestExample requestExample = Assert.Single(endpoint.RequestExamples);
            Assert.Equal("POST", requestExample.Method);
            Assert.Equal("/v1/widgets", requestExample.Path);
            Assert.Contains("\"name\": \"Alpha\"", requestExample.Body, StringComparison.Ordinal);
            HtmlCrawlStructuredResponseExample responseExample = Assert.Single(endpoint.ResponseExamples);
            Assert.Equal(201, responseExample.StatusCode);

            IDictionary<string, object?> paths = Assert.IsAssignableFrom<IDictionary<string, object?>>(result.OpenApiDocument["paths"]);
            IDictionary<string, object?> widgetsPath = Assert.IsAssignableFrom<IDictionary<string, object?>>(paths["/v1/widgets"]);
            IDictionary<string, object?> postOperation = Assert.IsAssignableFrom<IDictionary<string, object?>>(widgetsPath["post"]);
            Assert.True(postOperation.ContainsKey("requestBody"));
        } finally {
            DisposeListenerSafely(server);
        }
    }

    [Fact]
    public async Task CrawlAsync_IncludeStructuredJson_StripsQueryStringsAndPreservesCookieParameters() {
        Dictionary<string, string> responses = new() {
            ["/docs/session/get"] = """
            <html>
              <head>
                <title>Get session</title>
              </head>
              <body>
                <main>
                  <h1>Get session</h1>
                  <h2>GET https://api.example.com/v1/session?expand=user</h2>
                  <h3>Parameters</h3>
                  <table class="parameters">
                    <tr><th>Name</th><th>In</th><th>Type</th><th>Required</th><th>Description</th></tr>
                    <tr><td>session_id</td><td>cookie</td><td>string</td><td>Yes</td><td>Session cookie.</td></tr>
                  </table>
                  <h3>Response 200</h3>
                  <pre><code class="language-json">{ "id": "sess_123" }</code></pre>
                </main>
              </body>
            </html>
            """
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl + "docs/session/get", new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Selector = "main",
                IncludeStructuredJson = true
            });

            HtmlCrawlStructuredJson structuredJson = Assert.Single(result.Pages).StructuredJson!;
            HtmlCrawlStructuredApiEndpoint endpoint = Assert.Single(structuredJson.ApiEndpoints);
            Assert.Equal("/v1/session", endpoint.Path);
            HtmlCrawlStructuredApiParameter cookieParameter = Assert.Single(endpoint.Parameters);
            Assert.Equal("cookie", cookieParameter.Location);

            IDictionary<string, object?> paths = Assert.IsAssignableFrom<IDictionary<string, object?>>(result.OpenApiDocument["paths"]);
            Assert.True(paths.ContainsKey("/v1/session"));
            Assert.False(paths.ContainsKey("/v1/session?expand=user"));
            IDictionary<string, object?> sessionPath = Assert.IsAssignableFrom<IDictionary<string, object?>>(paths["/v1/session"]);
            IDictionary<string, object?> getOperation = Assert.IsAssignableFrom<IDictionary<string, object?>>(sessionPath["get"]);
            List<object> parameters = Assert.IsAssignableFrom<List<object>>(getOperation["parameters"]);
            IDictionary<string, object?> strictCookieParameter = Assert.IsAssignableFrom<IDictionary<string, object?>>(Assert.Single(parameters));
            Assert.Equal("session_id", strictCookieParameter["name"] as string);
            Assert.Equal("cookie", strictCookieParameter["in"] as string);
        } finally {
            DisposeListenerSafely(server);
        }
    }

    [Fact]
    public async Task CrawlAsync_IncludeStructuredJson_InfersCookieLocationFromTableHeading() {
        Dictionary<string, string> responses = new() {
            ["/docs/session/delete"] = """
            <html>
              <head>
                <title>Delete session</title>
              </head>
              <body>
                <main>
                  <h1>Delete session</h1>
                  <h2>DELETE /v1/session</h2>
                  <h3>Cookie parameters</h3>
                  <table class="parameters">
                    <tr><th>Name</th><th>Type</th><th>Required</th><th>Description</th></tr>
                    <tr><td>session_id</td><td>string</td><td>Yes</td><td>Session cookie.</td></tr>
                  </table>
                  <h3>Response 204</h3>
                  <pre><code class="language-http">HTTP/1.1 204 No Content</code></pre>
                </main>
              </body>
            </html>
            """
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl + "docs/session/delete", new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Selector = "main",
                IncludeStructuredJson = true
            });

            HtmlCrawlStructuredJson structuredJson = Assert.Single(result.Pages).StructuredJson!;
            HtmlCrawlStructuredApiEndpoint endpoint = Assert.Single(structuredJson.ApiEndpoints);
            HtmlCrawlStructuredApiParameter cookieParameter = Assert.Single(endpoint.Parameters);
            Assert.Equal("cookie", cookieParameter.Location);

            IDictionary<string, object?> paths = Assert.IsAssignableFrom<IDictionary<string, object?>>(result.OpenApiDocument["paths"]);
            IDictionary<string, object?> sessionPath = Assert.IsAssignableFrom<IDictionary<string, object?>>(paths["/v1/session"]);
            IDictionary<string, object?> deleteOperation = Assert.IsAssignableFrom<IDictionary<string, object?>>(sessionPath["delete"]);
            List<object> parameters = Assert.IsAssignableFrom<List<object>>(deleteOperation["parameters"]);
            IDictionary<string, object?> strictCookieParameter = Assert.IsAssignableFrom<IDictionary<string, object?>>(Assert.Single(parameters));
            Assert.Equal("cookie", strictCookieParameter["in"] as string);
        } finally {
            DisposeListenerSafely(server);
        }
    }

    [Fact]
    public async Task CrawlAsync_IncludeStructuredJson_MergesResolvedParameterLocationsAndAuthEvidenceAcrossPages() {
        Dictionary<string, string> responses = new() {
            ["/docs/widgets/reference"] = """
            <html>
              <head>
                <title>Widget reference</title>
              </head>
              <body>
                <main>
                  <h1>Widget reference</h1>
                  <a href="/docs/widgets/auth">Authentication details</a>
                  <h2>GET /v1/widgets/{id}</h2>
                  <p>No authentication required.</p>
                  <h3>Parameters</h3>
                  <table class="parameters">
                    <tr><th>Name</th><th>Type</th><th>Required</th><th>Description</th></tr>
                    <tr><td>id</td><td>string</td><td>Yes</td><td>Widget identifier.</td></tr>
                  </table>
                  <h3>Response 200</h3>
                  <pre><code class="language-json">{ "id": "wid_123" }</code></pre>
                </main>
              </body>
            </html>
            """,
            ["/docs/widgets/auth"] = """
            <html>
              <head>
                <title>Widget auth</title>
              </head>
              <body>
                <main>
                  <h1>Widget auth</h1>
                  <h2>GET /v1/widgets/{id}</h2>
                  <p>Bearer token required for all requests.</p>
                  <h3>Parameters</h3>
                  <table class="parameters">
                    <tr><th>Name</th><th>In</th><th>Type</th><th>Required</th><th>Description</th></tr>
                    <tr><td>id</td><td>path</td><td>string</td><td>Yes</td><td>Widget identifier.</td></tr>
                  </table>
                  <h3>Response 200</h3>
                  <pre><code class="language-json">{ "id": "wid_123", "name": "Alpha" }</code></pre>
                </main>
              </body>
            </html>
            """
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl + "docs/widgets/reference", new HtmlCrawlOptions {
                MaxDepth = 1,
                MaxPages = 3,
                Selector = "main",
                IncludeStructuredJson = true
            });

            HtmlCrawlStructuredOpenApiOperation operation = result.OpenApiLike.Paths["/v1/widgets/{id}"].Operations["get"];
            Assert.True(operation.Authentication.Required);
            Assert.Contains("bearer", operation.Authentication.Schemes, StringComparer.OrdinalIgnoreCase);

            IDictionary<string, object?> paths = Assert.IsAssignableFrom<IDictionary<string, object?>>(result.OpenApiDocument["paths"]);
            IDictionary<string, object?> widgetsPath = Assert.IsAssignableFrom<IDictionary<string, object?>>(paths["/v1/widgets/{id}"]);
            IDictionary<string, object?> getOperation = Assert.IsAssignableFrom<IDictionary<string, object?>>(widgetsPath["get"]);
            Assert.True(getOperation.ContainsKey("security"));
            List<object> parameters = Assert.IsAssignableFrom<List<object>>(getOperation["parameters"]);
            IDictionary<string, object?> idParameter = Assert.IsAssignableFrom<IDictionary<string, object?>>(Assert.Single(parameters));
            Assert.Equal("id", idParameter["name"] as string);
            Assert.Equal("path", idParameter["in"] as string);
        } finally {
            DisposeListenerSafely(server);
        }
    }

    [Fact]
    public async Task CrawlAsync_IncludeStructuredJson_DoesNotCreateStrictRequestBodyForBodylessRequestExamples() {
        Dictionary<string, string> responses = new() {
            ["/docs/items/list"] = """
            <html>
              <head>
                <title>List items</title>
              </head>
              <body>
                <main>
                  <h1>List items</h1>
                  <h2>GET /v1/items</h2>
                  <pre><code class="language-http">GET /v1/items HTTP/1.1
                  Host: api.example.com
                  Accept: application/json</code></pre>
                  <h3>Response 200</h3>
                  <pre><code class="language-json">[{ "id": "itm_123" }]</code></pre>
                </main>
              </body>
            </html>
            """
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl + "docs/items/list", new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Selector = "main",
                IncludeStructuredJson = true
            });

            HtmlCrawlStructuredJson structuredJson = Assert.Single(result.Pages).StructuredJson!;
            HtmlCrawlStructuredApiEndpoint endpoint = Assert.Single(structuredJson.ApiEndpoints);
            HtmlCrawlStructuredRequestExample requestExample = Assert.Single(endpoint.RequestExamples);
            Assert.True(string.IsNullOrWhiteSpace(requestExample.Body));

            IDictionary<string, object?> paths = Assert.IsAssignableFrom<IDictionary<string, object?>>(result.OpenApiDocument["paths"]);
            IDictionary<string, object?> itemsPath = Assert.IsAssignableFrom<IDictionary<string, object?>>(paths["/v1/items"]);
            IDictionary<string, object?> getOperation = Assert.IsAssignableFrom<IDictionary<string, object?>>(itemsPath["get"]);
            Assert.False(getOperation.ContainsKey("requestBody"));
        } finally {
            DisposeListenerSafely(server);
        }
    }

    [Fact]
    public async Task CrawlAsync_DatasetScenario_AutoPreset_ResolvesProductPages() {
        Dictionary<string, string> responses = new() {
            ["/products/widget"] = """
            <html>
              <head>
                <title>Widget 3000</title>
                <meta property="og:image" content="https://cdn.example.com/widget.png">
              </head>
              <body>
                <nav class="breadcrumbs" aria-label="Breadcrumb">
                  <a href="/products">Products</a>
                  <a href="/products/widget">Widget 3000</a>
                </nav>
                <main itemscope itemtype="https://schema.org/Product">
                  <h1 itemprop="name">Widget 3000</h1>
                  <div class="price">$19.99</div>
                  <div class="sku">SKU-3000</div>
                  <div class="availability">In stock</div>
                  <button>Add to cart</button>
                  <table class="specs">
                    <tr><th>Feature</th><th>Value</th></tr>
                    <tr><td>Weight</td><td>1kg</td></tr>
                    <tr><td>Color</td><td>Blue</td></tr>
                  </table>
                  <p>Compact widget for offline crawling demos.</p>
                </main>
              </body>
            </html>
            """
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        string outputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl + "products/widget", new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Scenario = HtmlCrawlScenario.Dataset,
                Selector = "main",
                OutputPath = outputPath
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.NotNull(page.StructuredJson);
            Assert.Equal(HtmlCrawlStructuredJsonPreset.Product, page.StructuredJson!.ResolvedPreset);
            Assert.Single(page.StructuredJson.SpecTables);
            Assert.Single(page.StructuredJson.PrimaryActions);
            Assert.Equal("Widget 3000", page.StructuredJson.Extracted["name"]);
            Assert.Equal("$19.99", page.StructuredJson.Extracted["price"]);
            Assert.Equal("SKU-3000", page.StructuredJson.Extracted["sku"]);
            Assert.Equal("In stock", page.StructuredJson.Extracted["availability"]);
            IList<HtmlCrawlStructuredSpecTable> specTables = Assert.IsAssignableFrom<IList<HtmlCrawlStructuredSpecTable>>(page.StructuredJson.Extracted["specTables"]);
            Assert.Single(specTables);
            IList<HtmlCrawlStructuredPrimaryAction> primaryActions = Assert.IsAssignableFrom<IList<HtmlCrawlStructuredPrimaryAction>>(page.StructuredJson.Extracted["primaryActions"]);
            Assert.Single(primaryActions);
        } finally {
            DisposeListenerSafely(server);
            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CrawlAsync_StructuredSchema_ExtractsCallerDefinedFields() {
        Dictionary<string, string> responses = new() {
            ["/"] = """
            <html>
              <head>
                <title>Schema Example</title>
                <meta name="description" content="Schema description">
              </head>
              <body>
                <header>
                  <nav>
                    <a href="/home">Home</a>
                    <a href="/docs">Docs</a>
                  </nav>
                </header>
                <main>
                  <h1>Schema page</h1>
                  <p>Schema body.</p>
                </main>
                <footer>Footer text</footer>
              </body>
            </html>
            """
        };

        const string schema = """
        {
          "title": "Metadata.Title",
          "description": "Metadata.Description",
          "navLinks": {
            "selector": "nav a",
            "source": "page",
            "mode": "text",
            "all": true
          },
          "mainHeading": {
            "selector": "h1",
            "source": "selected",
            "mode": "text"
          },
          "firstHeading": "Document.Headings.0",
          "hasFooter": {
            "selector": "footer",
            "source": "page",
            "mode": "exists"
          }
        }
        """;

        HttpListener server = StartServer(responses, out string rootUrl);
        string outputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Selector = "main",
                StructuredJsonSchema = schema,
                OutputPath = outputPath
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.NotNull(page.StructuredJson);
            Assert.Equal("Schema Example", page.StructuredJson!.Extracted["title"]);
            Assert.Equal("Schema description", page.StructuredJson.Extracted["description"]);
            List<object?> navLinks = Assert.IsType<List<object?>>(page.StructuredJson.Extracted["navLinks"]);
            Assert.Equal(new object?[] { "Home", "Docs" }, navLinks);
            Assert.Equal("Schema page", page.StructuredJson.Extracted["mainHeading"]);
            Assert.Equal("Schema page", page.StructuredJson.Extracted["firstHeading"]);
            Assert.Equal(true, page.StructuredJson.Extracted["hasFooter"]);
        } finally {
            DisposeListenerSafely(server);
            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }
}
