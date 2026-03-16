using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlCrawlerStructuredJsonTests {
    private static int GetFreePort() {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static HttpListener StartServer(Dictionary<string, string> responses, out string rootUrl) {
        int port = GetFreePort();
        rootUrl = $"http://localhost:{port}/";
        HttpListener listener = new();
        listener.Prefixes.Add(rootUrl);
        listener.Start();

        _ = Task.Run(async () => {
            try {
                while (listener.IsListening) {
                    HttpListenerContext context = await listener.GetContextAsync().ConfigureAwait(false);
                    string key = context.Request.RawUrl ?? "/";
                    if (responses.TryGetValue(key, out string? html)) {
                        byte[] data = Encoding.UTF8.GetBytes(html);
                        context.Response.ContentType = "text/html; charset=utf-8";
                        context.Response.ContentLength64 = data.Length;
                        await context.Response.OutputStream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
                    } else {
                        context.Response.StatusCode = 404;
                    }

                    context.Response.OutputStream.Close();
                }
            } catch (HttpListenerException) {
            } catch (ObjectDisposedException) {
            }
        });

        return listener;
    }

    [Fact]
    public async Task CrawlAsync_IncludeStructuredJson_PopulatesStructuredJsonAndPersistsFiles() {
        Dictionary<string, string> responses = new() {
            ["/"] = """
            <html lang="en">
              <head>
                <title>Example</title>
                <link rel="canonical" href="/canonical-page">
                <meta name="description" content="Example page">
                <meta name="author" content="Jane Doe">
                <meta name="keywords" content="example, structured, crawl">
                <meta property="og:title" content="OG Example">
                <meta property="og:site_name" content="Example Docs">
                <meta property="og:type" content="article">
                <meta property="og:image" content="https://cdn.example.com/image.png">
              </head>
              <body>
                <header class="site-header">
                  <nav class="site-navigation" aria-label="Main menu">
                    <a href="/home">Home</a>
                    <a href="/docs">Docs</a>
                  </nav>
                </header>
                <nav class="breadcrumbs" aria-label="Breadcrumb">
                  <a href="/docs">Docs</a>
                  <span aria-current="page">Example page</span>
                </nav>
                <div itemscope itemtype="https://schema.org/Article">
                  <span itemprop="headline">Structured headline</span>
                </div>
                <main>
                  <h1>Example page</h1>
                  <div class="callout note">
                    <strong>Note</strong>
                    <p>Important offline behavior note.</p>
                  </div>
                  <a class="button primary" href="/install">Install now</a>
                  <ul><li>One</li><li>Two</li></ul>
                  <pre><code class="language-csharp">Write-Host "hello"</code></pre>
                  <h2>POST /v1/widgets</h2>
                  <p>Create a widget with a JSON body.</p>
                  <p>Authenticate with your API key using the X-API-Key header. Rate limit: 60 requests per minute. Exceeding the quota returns 429 with Retry-After.</p>
                  <h3>Headers</h3>
                  <table class="parameters">
                    <tr><th>Name</th><th>Type</th><th>Required</th><th>Example</th><th>Description</th></tr>
                    <tr><td>X-API-Key</td><td>string</td><td>Yes</td><td>sk_live_123</td><td>API key used for authentication.</td></tr>
                  </table>
                  <h3>Request body parameters</h3>
                  <table class="parameters">
                    <tr><th>Name</th><th>Type</th><th>Required</th><th>Nullable</th><th>Format</th><th>Enum</th><th>Example</th><th>Pattern</th><th>Description</th></tr>
                    <tr><td>name</td><td>string</td><td>Yes</td><td>No</td><td>slug</td><td>alpha,beta</td><td>alpha</td><td>^[a-z]+$</td><td>Widget slug. One of: alpha, beta.</td></tr>
                  </table>
                  <pre><code class="language-http">POST /v1/widgets HTTP/1.1
                  Content-Type: application/json

                  { "name": "Alpha" }</code></pre>
                  <h3>Response 201</h3>
                  <pre><code class="language-json">{
                  "id": "wid_123",
                  "name": "Alpha",
                  "meta": {
                    "createdAt": "2026-03-15T10:00:00Z"
                  },
                  "tags": ["alpha", "beta"]
                  }</code></pre>
                  <h3>Error 429</h3>
                  <pre><code class="language-http">HTTP/1.1 429 Too Many Requests
                  Retry-After: 60
                  X-RateLimit-Remaining: 0
                  Content-Type: application/json

                  {
                  "error": "rate_limited",
                  "details": {
                    "retryAfterSeconds": 60
                  }
                  }</code></pre>
                  <details>
                    <summary>What is this page?</summary>
                    <p>An example FAQ answer.</p>
                  </details>
                  <table>
                    <tr><th>Name</th><th>Value</th></tr>
                    <tr><td>Alpha</td><td>1</td></tr>
                  </table>
                  <form action="/submit" method="post">
                    <input type="text" name="user" />
                  </form>
                </main>
                <footer class="site-footer">
                  <a href="/privacy">Privacy</a>
                  <span>Copyright 2026</span>
                </footer>
              </body>
            </html>
            """
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        string outputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Selector = "main",
                IncludeStructuredJson = true,
                OutputPath = outputPath
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.NotNull(page.StructuredJson);
            Assert.Equal(7, page.StructuredJson!.MetaTags.Count);
            Assert.Equal(4, page.StructuredJson.OpenGraph.Properties.Count);
            Assert.Single(page.StructuredJson.MicrodataItems);
            Assert.Single(page.StructuredJson.Forms);
            Assert.Single(page.StructuredJson.Lists);
            Assert.Equal(3, page.StructuredJson.Tables.Count);
            Assert.Equal(4, page.StructuredJson.CodeBlocks.Count);
            Assert.Equal(4, page.StructuredJson.CodeSamples.Count);
            Assert.Single(page.StructuredJson.Breadcrumbs);
            Assert.Single(page.StructuredJson.FaqItems);
            Assert.Single(page.StructuredJson.SpecTables);
            Assert.Single(page.StructuredJson.Callouts);
            Assert.Single(page.StructuredJson.PrimaryActions);
            Assert.Single(page.StructuredJson.ApiEndpoints);
            Assert.Equal(rootUrl, page.StructuredJson.Document.Url);
            Assert.Equal("Example", page.StructuredJson.Document.Title);
            Assert.Equal(0, page.StructuredJson.Document.Depth);
            Assert.Contains("Example page", page.StructuredJson.Document.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("Home", page.StructuredJson.Document.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("Copyright", page.StructuredJson.Document.Text, StringComparison.Ordinal);
            Assert.Contains("# Example page", page.StructuredJson.Document.Markdown, StringComparison.Ordinal);
            Assert.Contains("One", page.StructuredJson.Document.Markdown, StringComparison.Ordinal);
            Assert.Contains("example", page.StructuredJson.Document.Keywords, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Example page", page.StructuredJson.Document.Headings, StringComparer.Ordinal);
            Assert.Contains("Structured headline", page.StructuredJson.MicrodataItems[0].Properties["headline"], StringComparer.Ordinal);
            Assert.Equal(page.StructuredJson.Document.WordCount, page.StructuredJson.Content.WordCount);
            Assert.Equal(page.StructuredJson.Document.Summary, page.StructuredJson.Content.Summary);
            Assert.Equal("Example page", page.StructuredJson.Metadata.Description);
            Assert.Equal(rootUrl + "canonical-page", page.StructuredJson.Metadata.CanonicalUrl);
            Assert.Equal("en", page.StructuredJson.Metadata.Language);
            Assert.Equal("Example Docs", page.StructuredJson.Metadata.SiteName);
            Assert.Equal("article", page.StructuredJson.Metadata.Type);
            Assert.Equal("Jane Doe", page.StructuredJson.Metadata.Author);
            Assert.Equal("https://cdn.example.com/image.png", page.StructuredJson.Metadata.ImageUrl);
            Assert.Contains("structured", page.StructuredJson.Metadata.Keywords, StringComparer.OrdinalIgnoreCase);
            Assert.Equal(1, page.StructuredJson.Layout.HeaderCount);
            Assert.Equal(1, page.StructuredJson.Layout.NavigationCount);
            Assert.Equal(1, page.StructuredJson.Layout.MainCount);
            Assert.Equal(1, page.StructuredJson.Layout.FooterCount);
            Assert.Contains(page.StructuredJson.Layout.Regions, region => string.Equals(region.Kind, "Navigation", StringComparison.OrdinalIgnoreCase) && region.LinkLabels.Contains("Home"));
            Assert.Contains(page.StructuredJson.Layout.Regions, region => string.Equals(region.Kind, "Footer", StringComparison.OrdinalIgnoreCase) && region.Summary.Contains("Copyright", StringComparison.Ordinal));
            Assert.Contains(page.StructuredJson.CodeBlocks, block => string.Equals(block.Language, "csharp", StringComparison.OrdinalIgnoreCase) && block.Code.Contains("Write-Host", StringComparison.Ordinal));
            Assert.Contains(page.StructuredJson.CodeSamples, sample => string.Equals(sample.Kind, "http", StringComparison.OrdinalIgnoreCase) && string.Equals(sample.Method, "POST", StringComparison.OrdinalIgnoreCase) && string.Equals(sample.Path, "/v1/widgets", StringComparison.Ordinal));
            Assert.Equal("POST", page.StructuredJson.ApiEndpoints[0].Method);
            Assert.Equal("/v1/widgets", page.StructuredJson.ApiEndpoints[0].Path);
            Assert.Contains("Create a widget", page.StructuredJson.ApiEndpoints[0].Description, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("postWidgets", page.StructuredJson.ApiEndpoints[0].OperationId);
            Assert.Equal("widgets", page.StructuredJson.ApiEndpoints[0].Resource);
            Assert.Contains("widgets", page.StructuredJson.ApiEndpoints[0].Tags, StringComparer.OrdinalIgnoreCase);
            Assert.Equal(2, page.StructuredJson.ApiEndpoints[0].Parameters.Count);
            Assert.Contains(page.StructuredJson.ApiEndpoints[0].Parameters, parameter => string.Equals(parameter.Name, "name", StringComparison.Ordinal) && string.Equals(parameter.Location, "body", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(page.StructuredJson.ApiEndpoints[0].Parameters, parameter => string.Equals(parameter.Name, "X-API-Key", StringComparison.Ordinal) && string.Equals(parameter.Location, "header", StringComparison.OrdinalIgnoreCase));
            HtmlCrawlStructuredApiParameter nameParameter = page.StructuredJson.ApiEndpoints[0].Parameters.First(parameter => string.Equals(parameter.Name, "name", StringComparison.Ordinal));
            Assert.Equal("slug", nameParameter.Format);
            Assert.False(nameParameter.Nullable ?? true);
            Assert.Equal("alpha", nameParameter.ExampleValue);
            Assert.Equal("^[a-z]+$", nameParameter.Pattern);
            Assert.Contains("alpha", nameParameter.EnumValues, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("beta", nameParameter.EnumValues, StringComparer.OrdinalIgnoreCase);
            HtmlCrawlStructuredApiParameter apiKeyParameter = page.StructuredJson.ApiEndpoints[0].Parameters.First(parameter => string.Equals(parameter.Name, "X-API-Key", StringComparison.Ordinal));
            Assert.Equal("sk_live_123", apiKeyParameter.ExampleValue);
            Assert.Empty(page.StructuredJson.ApiEndpoints[0].PathParameters);
            Assert.Empty(page.StructuredJson.ApiEndpoints[0].QueryParameters);
            Assert.Single(page.StructuredJson.ApiEndpoints[0].HeaderParameters);
            Assert.Single(page.StructuredJson.ApiEndpoints[0].BodyParameters);
            Assert.Equal("string", page.StructuredJson.ApiEndpoints[0].RequestBodySchema["name"]);
            Assert.Single(page.StructuredJson.ApiEndpoints[0].RequestBodyFields);
            Assert.Equal("name", page.StructuredJson.ApiEndpoints[0].RequestBodyFields[0].Path);
            Assert.Equal("slug", page.StructuredJson.ApiEndpoints[0].RequestBodyFields[0].Format);
            Assert.Equal("alpha", page.StructuredJson.ApiEndpoints[0].RequestBodyFields[0].ExampleValue);
            Assert.True(page.StructuredJson.ApiEndpoints[0].RequestBodyFields[0].ConfidenceScore > 0);
            Assert.True(page.StructuredJson.ApiEndpoints[0].RequestBodyFields[0].EvidenceCount > 0);
            Assert.Contains(page.StructuredJson.ApiEndpoints[0].RequestBodyFields[0].Provenance, entry => string.Equals(entry.Kind, "ParameterTable", StringComparison.OrdinalIgnoreCase) && string.Equals(entry.PageUrl, page.Url, StringComparison.OrdinalIgnoreCase) && string.Equals(entry.Label, "name", StringComparison.Ordinal));
            Assert.True(page.StructuredJson.ApiEndpoints[0].Authentication.Required ?? false);
            Assert.Contains("api-key", page.StructuredJson.ApiEndpoints[0].Authentication.Schemes, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("X-API-Key", page.StructuredJson.ApiEndpoints[0].Authentication.Headers, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Authenticate with your API key", page.StructuredJson.ApiEndpoints[0].Authentication.Summary, StringComparison.OrdinalIgnoreCase);
            Assert.True(page.StructuredJson.ApiEndpoints[0].RateLimit.Mentioned);
            Assert.Equal(429, page.StructuredJson.ApiEndpoints[0].RateLimit.StatusCode);
            Assert.Equal("60 requests per minute", page.StructuredJson.ApiEndpoints[0].RateLimit.Limit);
            Assert.Equal("minute", page.StructuredJson.ApiEndpoints[0].RateLimit.Window);
            Assert.Contains("Retry-After", page.StructuredJson.ApiEndpoints[0].RateLimit.Headers, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Rate limit", page.StructuredJson.ApiEndpoints[0].RateLimit.Summary, StringComparison.OrdinalIgnoreCase);
            Assert.Single(page.StructuredJson.ApiEndpoints[0].RequestExamples);
            Assert.Equal("POST", page.StructuredJson.ApiEndpoints[0].RequestExamples[0].Method);
            Assert.Equal("/v1/widgets", page.StructuredJson.ApiEndpoints[0].RequestExamples[0].Path);
            Assert.Equal("application/json", page.StructuredJson.ApiEndpoints[0].RequestExamples[0].ContentType);
            Assert.Contains("\"name\": \"Alpha\"", page.StructuredJson.ApiEndpoints[0].RequestExamples[0].Body, StringComparison.Ordinal);
            Assert.Contains(page.StructuredJson.ApiEndpoints[0].RequestHeaders, header => string.Equals(header.Name, "Content-Type", StringComparison.OrdinalIgnoreCase) && string.Equals(header.Value, "application/json", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(page.StructuredJson.ApiEndpoints[0].RequestHeaders, header => string.Equals(header.Name, "X-API-Key", StringComparison.OrdinalIgnoreCase) && string.Equals(header.Value, "sk_live_123", StringComparison.Ordinal));
            Assert.Equal(2, page.StructuredJson.ApiEndpoints[0].ResponseExamples.Count);
            Assert.Single(page.StructuredJson.ApiEndpoints[0].ErrorResponses);
            Assert.Equal(429, page.StructuredJson.ApiEndpoints[0].ErrorResponses[0].StatusCode);
            Assert.Equal("Too Many Requests", page.StructuredJson.ApiEndpoints[0].ErrorResponses[0].StatusText);
            Assert.True(page.StructuredJson.ApiEndpoints[0].ErrorResponses[0].IsError);
            Assert.Contains(page.StructuredJson.ApiEndpoints[0].ErrorResponses[0].Headers, header => string.Equals(header.Name, "Retry-After", StringComparison.OrdinalIgnoreCase) && string.Equals(header.Value, "60", StringComparison.Ordinal));
            Assert.Contains("\"error\"", page.StructuredJson.ApiEndpoints[0].ErrorResponses[0].Body, StringComparison.Ordinal);
            Assert.Single(page.StructuredJson.ApiEndpoints[0].ErrorCatalog);
            Assert.Equal(429, page.StructuredJson.ApiEndpoints[0].ErrorCatalog[0].StatusCode);
            Assert.Equal("Too Many Requests", page.StructuredJson.ApiEndpoints[0].ErrorCatalog[0].StatusText);
            Assert.Contains(page.StructuredJson.ApiEndpoints[0].ErrorCatalog[0].Headers, header => string.Equals(header.Name, "Retry-After", StringComparison.OrdinalIgnoreCase));
            Assert.Equal("string", page.StructuredJson.ApiEndpoints[0].ErrorCatalog[0].Schema["error"]);
            Assert.Contains(page.StructuredJson.ApiEndpoints[0].ErrorCatalog[0].Fields, field => string.Equals(field.Path, "details", StringComparison.Ordinal) && string.Equals(field.Kind, "object", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(page.StructuredJson.ApiEndpoints[0].ResponseHeaders, header => string.Equals(header.Name, "Retry-After", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(page.StructuredJson.ApiEndpoints[0].ResponseHeaders, header => string.Equals(header.Name, "X-RateLimit-Remaining", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(page.StructuredJson.ApiEndpoints[0].ResponseExamples, response => response.StatusCode == 201 && response.ContentType == "application/json" && response.Body.Contains("\"id\"", StringComparison.Ordinal));
            Assert.Contains(page.StructuredJson.ApiEndpoints[0].ResponseExamples, response => response.StatusCode == 201 && response.TopLevelKeys.Contains("id") && response.TopLevelKeys.Contains("name"));
            Assert.Equal("string", page.StructuredJson.ApiEndpoints[0].ResponseExamples.First(response => response.StatusCode == 201).BodySchema["id"]);
            Assert.Equal("string", page.StructuredJson.ApiEndpoints[0].ResponseExamples.First(response => response.StatusCode == 201).BodySchema["name"]);
            Assert.Equal("object", page.StructuredJson.ApiEndpoints[0].ResponseExamples.First(response => response.StatusCode == 201).BodySchema["meta"]);
            Assert.Equal("string", page.StructuredJson.ApiEndpoints[0].ResponseExamples.First(response => response.StatusCode == 201).BodySchema["meta.createdAt"]);
            Assert.Equal("array", page.StructuredJson.ApiEndpoints[0].ResponseExamples.First(response => response.StatusCode == 201).BodySchema["tags"]);
            Assert.Equal("string", page.StructuredJson.ApiEndpoints[0].ResponseExamples.First(response => response.StatusCode == 201).BodySchema["tags[]"]);
            Assert.Contains(page.StructuredJson.ApiEndpoints[0].ResponseExamples.First(response => response.StatusCode == 201).BodyFields, field => string.Equals(field.Path, "id", StringComparison.Ordinal) && string.Equals(field.ExampleValue, "wid_123", StringComparison.Ordinal));
            Assert.True(page.StructuredJson.ApiEndpoints[0].ResponseExamples.First(response => response.StatusCode == 201).BodyFields.First(field => string.Equals(field.Path, "id", StringComparison.Ordinal)).ConfidenceScore > 0);
            Assert.Contains(page.StructuredJson.ApiEndpoints[0].ResponseExamples.First(response => response.StatusCode == 201).BodyFields.First(field => string.Equals(field.Path, "id", StringComparison.Ordinal)).Provenance, entry => string.Equals(entry.Kind, "JsonResponse", StringComparison.OrdinalIgnoreCase) && string.Equals(entry.PageUrl, page.Url, StringComparison.OrdinalIgnoreCase));
            Assert.Contains(page.StructuredJson.ApiEndpoints[0].ResponseExamples.First(response => response.StatusCode == 201).BodyFields, field => string.Equals(field.Path, "meta", StringComparison.Ordinal) && string.Equals(field.Kind, "object", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(page.StructuredJson.ApiEndpoints[0].ResponseExamples.First(response => response.StatusCode == 201).BodyFields, field => string.Equals(field.Path, "meta.createdAt", StringComparison.Ordinal) && string.Equals(field.ParentPath, "meta", StringComparison.Ordinal));
            Assert.Contains(page.StructuredJson.ApiEndpoints[0].ResponseExamples.First(response => response.StatusCode == 201).BodyFields, field => string.Equals(field.Path, "tags", StringComparison.Ordinal) && string.Equals(field.Kind, "array", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(page.StructuredJson.ApiEndpoints[0].ResponseExamples.First(response => response.StatusCode == 201).BodyFields, field => string.Equals(field.Path, "tags[]", StringComparison.Ordinal) && string.Equals(field.ParentPath, "tags", StringComparison.Ordinal));
            Assert.Equal("application/json", page.StructuredJson.ApiEndpoints[0].ErrorResponses[0].ContentType);
            Assert.Equal("string", page.StructuredJson.ApiEndpoints[0].ErrorResponses[0].BodySchema["error"]);
            Assert.Equal("object", page.StructuredJson.ApiEndpoints[0].ErrorResponses[0].BodySchema["details"]);
            Assert.Equal("integer", page.StructuredJson.ApiEndpoints[0].ErrorResponses[0].BodySchema["details.retryAfterSeconds"]);
            Assert.Equal("string", page.StructuredJson.ApiEndpoints[0].SuccessResponseSchema["id"]);
            Assert.Equal("string", page.StructuredJson.ApiEndpoints[0].SuccessResponseSchema["name"]);
            Assert.Equal("object", page.StructuredJson.ApiEndpoints[0].SuccessResponseSchema["meta"]);
            Assert.Equal("string", page.StructuredJson.ApiEndpoints[0].SuccessResponseSchema["meta.createdAt"]);
            Assert.Equal("array", page.StructuredJson.ApiEndpoints[0].SuccessResponseSchema["tags"]);
            Assert.Equal("string", page.StructuredJson.ApiEndpoints[0].SuccessResponseSchema["tags[]"]);
            Assert.Equal("string", page.StructuredJson.ApiEndpoints[0].ErrorResponseSchema["error"]);
            Assert.Equal("object", page.StructuredJson.ApiEndpoints[0].ErrorResponseSchema["details"]);
            Assert.Equal("integer", page.StructuredJson.ApiEndpoints[0].ErrorResponseSchema["details.retryAfterSeconds"]);
            Assert.Contains(page.StructuredJson.ApiEndpoints[0].SuccessResponseFields, field => string.Equals(field.Path, "id", StringComparison.Ordinal) && string.Equals(field.Type, "string", StringComparison.OrdinalIgnoreCase));
            Assert.True(page.StructuredJson.ApiEndpoints[0].SuccessResponseFields.First(field => string.Equals(field.Path, "id", StringComparison.Ordinal)).ConfidenceScore > 0);
            Assert.Contains(page.StructuredJson.ApiEndpoints[0].SuccessResponseFields.First(field => string.Equals(field.Path, "id", StringComparison.Ordinal)).Provenance, entry => string.Equals(entry.Kind, "JsonResponse", StringComparison.OrdinalIgnoreCase) && string.Equals(entry.PageUrl, page.Url, StringComparison.OrdinalIgnoreCase));
            Assert.Contains(page.StructuredJson.ApiEndpoints[0].SuccessResponseFields, field => string.Equals(field.Path, "meta", StringComparison.Ordinal) && string.Equals(field.Kind, "object", StringComparison.OrdinalIgnoreCase) && field.ChildPaths.Contains("meta.createdAt"));
            Assert.Contains(page.StructuredJson.ApiEndpoints[0].SuccessResponseFields, field => string.Equals(field.Path, "tags", StringComparison.Ordinal) && string.Equals(field.Kind, "array", StringComparison.OrdinalIgnoreCase) && field.ChildPaths.Contains("tags[]"));
            Assert.Contains(page.StructuredJson.ApiEndpoints[0].ErrorResponseFields, field => string.Equals(field.Path, "error", StringComparison.Ordinal) && string.Equals(field.ExampleValue, "rate_limited", StringComparison.Ordinal));
            Assert.Contains(page.StructuredJson.ApiEndpoints[0].ErrorResponseFields.First(field => string.Equals(field.Path, "error", StringComparison.Ordinal)).Provenance, entry => string.Equals(entry.Kind, "JsonResponse", StringComparison.OrdinalIgnoreCase) && string.Equals(entry.PageUrl, page.Url, StringComparison.OrdinalIgnoreCase));
            Assert.Contains(page.StructuredJson.ApiEndpoints[0].ErrorResponseFields, field => string.Equals(field.Path, "details", StringComparison.Ordinal) && string.Equals(field.Kind, "object", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(new[] { "Docs", "Example page" }, page.StructuredJson.Breadcrumbs[0].Labels);
            Assert.Equal("What is this page?", page.StructuredJson.FaqItems[0].Question);
            Assert.Contains("example FAQ answer", page.StructuredJson.FaqItems[0].Answer, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("Alpha", page.StructuredJson.SpecTables[0].Entries[0].Name);
            Assert.Equal("1", page.StructuredJson.SpecTables[0].Entries[0].Value);
            Assert.Equal("note", page.StructuredJson.Callouts[0].Kind);
            Assert.Contains("offline behavior note", page.StructuredJson.Callouts[0].Text, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("Install now", page.StructuredJson.PrimaryActions[0].Label);
            Assert.Equal(rootUrl + "install", page.StructuredJson.PrimaryActions[0].Url);
            Assert.Equal(1, page.StructuredJson.ApiCatalog.OperationCount);
            Assert.Equal(1, page.StructuredJson.ApiCatalog.PathCount);
            Assert.Equal(1, page.StructuredJson.ApiCatalog.AuthenticatedOperationCount);
            Assert.Equal(1, page.StructuredJson.ApiCatalog.RateLimitedOperationCount);
            Assert.Equal(1, page.StructuredJson.ApiCatalog.ErrorCatalogCount);
            Assert.Contains("widgets", page.StructuredJson.ApiCatalog.Resources, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("widgets", page.StructuredJson.ApiCatalog.Tags, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("postWidgets", page.StructuredJson.ApiCatalog.OperationIds, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(rootUrl.TrimEnd('/'), page.StructuredJson.OpenApiLike.Servers, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("widgets", page.StructuredJson.OpenApiLike.Tags, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("widgets", page.StructuredJson.OpenApiLike.Resources, StringComparer.OrdinalIgnoreCase);
            Assert.True(page.StructuredJson.OpenApiLike.Paths.ContainsKey("/v1/widgets"));
            Assert.True(page.StructuredJson.OpenApiLike.Paths["/v1/widgets"].Operations.ContainsKey("post"));
            Assert.Equal("postWidgets", page.StructuredJson.OpenApiLike.Paths["/v1/widgets"].Operations["post"].OperationId);
            Assert.Equal("widgets", page.StructuredJson.OpenApiLike.Paths["/v1/widgets"].Operations["post"].Resource);
            Assert.True(page.StructuredJson.OpenApiLike.Paths["/v1/widgets"].Operations["post"].Authentication.Required ?? false);
            Assert.False(string.IsNullOrWhiteSpace(page.StructuredJson.OpenApiLike.Paths["/v1/widgets"].Operations["post"].AuthenticationRef));
            Assert.False(string.IsNullOrWhiteSpace(page.StructuredJson.OpenApiLike.Paths["/v1/widgets"].Operations["post"].RateLimitRef));
            Assert.False(string.IsNullOrWhiteSpace(page.StructuredJson.OpenApiLike.Paths["/v1/widgets"].Operations["post"].ParametersRef));
            Assert.False(string.IsNullOrWhiteSpace(page.StructuredJson.OpenApiLike.Paths["/v1/widgets"].Operations["post"].RequestHeadersRef));
            Assert.False(string.IsNullOrWhiteSpace(page.StructuredJson.OpenApiLike.Paths["/v1/widgets"].Operations["post"].ResponseHeadersRef));
            Assert.False(string.IsNullOrWhiteSpace(page.StructuredJson.OpenApiLike.Paths["/v1/widgets"].Operations["post"].RequestExamplesRef));
            Assert.False(string.IsNullOrWhiteSpace(page.StructuredJson.OpenApiLike.Paths["/v1/widgets"].Operations["post"].ResponseExamplesRef));
            Assert.False(string.IsNullOrWhiteSpace(page.StructuredJson.OpenApiLike.Paths["/v1/widgets"].Operations["post"].ErrorCatalogRef));
            Assert.False(string.IsNullOrWhiteSpace(page.StructuredJson.OpenApiLike.Paths["/v1/widgets"].Operations["post"].RequestBodySchemaRef));
            Assert.False(string.IsNullOrWhiteSpace(page.StructuredJson.OpenApiLike.Paths["/v1/widgets"].Operations["post"].RequestBodyFieldsRef));
            Assert.False(string.IsNullOrWhiteSpace(page.StructuredJson.OpenApiLike.Paths["/v1/widgets"].Operations["post"].SuccessResponseSchemaRef));
            Assert.False(string.IsNullOrWhiteSpace(page.StructuredJson.OpenApiLike.Paths["/v1/widgets"].Operations["post"].SuccessResponseFieldsRef));
            Assert.False(string.IsNullOrWhiteSpace(page.StructuredJson.OpenApiLike.Paths["/v1/widgets"].Operations["post"].ErrorResponseSchemaRef));
            Assert.False(string.IsNullOrWhiteSpace(page.StructuredJson.OpenApiLike.Paths["/v1/widgets"].Operations["post"].ErrorResponseFieldsRef));
            Assert.Equal("string", page.StructuredJson.OpenApiLike.Paths["/v1/widgets"].Operations["post"].RequestBodySchema["name"]);
            Assert.Equal("string", page.StructuredJson.OpenApiLike.Paths["/v1/widgets"].Operations["post"].SuccessResponseSchema["id"]);
            Assert.Equal("string", page.StructuredJson.OpenApiLike.Paths["/v1/widgets"].Operations["post"].ErrorResponseSchema["error"]);
            Assert.NotEmpty(page.StructuredJson.OpenApiLike.Components.Schemas);
            Assert.NotEmpty(page.StructuredJson.OpenApiLike.Components.FieldSets);
            Assert.NotEmpty(page.StructuredJson.OpenApiLike.Components.AuthProfiles);
            Assert.NotEmpty(page.StructuredJson.OpenApiLike.Components.RateLimitProfiles);
            Assert.NotEmpty(page.StructuredJson.OpenApiLike.Components.ParameterSets);
            Assert.NotEmpty(page.StructuredJson.OpenApiLike.Components.RequestHeaderSets);
            Assert.NotEmpty(page.StructuredJson.OpenApiLike.Components.ResponseHeaderSets);
            Assert.NotEmpty(page.StructuredJson.OpenApiLike.Components.RequestExampleSets);
            Assert.NotEmpty(page.StructuredJson.OpenApiLike.Components.ResponseExampleSets);
            Assert.NotEmpty(page.StructuredJson.OpenApiLike.Components.ErrorCatalogs);

            Assert.False(string.IsNullOrWhiteSpace(page.StructuredJsonPath));
            Assert.True(File.Exists(page.StructuredJsonPath!));
            Assert.False(string.IsNullOrWhiteSpace(result.StructuredJsonPagesJsonlPath));
            Assert.True(File.Exists(result.StructuredJsonPagesJsonlPath!));

            string pageStructuredJson = File.ReadAllText(page.StructuredJsonPath!);
            Assert.Contains("\"Document\"", pageStructuredJson, StringComparison.Ordinal);
            Assert.Contains("\"Content\"", pageStructuredJson, StringComparison.Ordinal);
            Assert.Contains("\"Metadata\"", pageStructuredJson, StringComparison.Ordinal);
            Assert.Contains("\"Layout\"", pageStructuredJson, StringComparison.Ordinal);
            Assert.Contains("\"CodeBlocks\"", pageStructuredJson, StringComparison.Ordinal);
            Assert.Contains("\"CodeSamples\"", pageStructuredJson, StringComparison.Ordinal);
            Assert.Contains("\"ApiEndpoints\"", pageStructuredJson, StringComparison.Ordinal);
            Assert.Contains("\"Breadcrumbs\"", pageStructuredJson, StringComparison.Ordinal);
            Assert.Contains("\"FaqItems\"", pageStructuredJson, StringComparison.Ordinal);
            Assert.Contains("\"SpecTables\"", pageStructuredJson, StringComparison.Ordinal);
            Assert.Contains("\"Callouts\"", pageStructuredJson, StringComparison.Ordinal);
            Assert.Contains("\"PrimaryActions\"", pageStructuredJson, StringComparison.Ordinal);
            Assert.Contains("\"ApiCatalog\"", pageStructuredJson, StringComparison.Ordinal);
            Assert.Contains("\"OpenApiLike\"", pageStructuredJson, StringComparison.Ordinal);
            Assert.Contains("\"Authentication\"", pageStructuredJson, StringComparison.Ordinal);
            Assert.Contains("\"RateLimit\"", pageStructuredJson, StringComparison.Ordinal);
            Assert.Contains("\"RequestExamples\"", pageStructuredJson, StringComparison.Ordinal);
            Assert.Contains("\"RequestHeaders\"", pageStructuredJson, StringComparison.Ordinal);
            Assert.Contains("\"ErrorResponses\"", pageStructuredJson, StringComparison.Ordinal);
            Assert.Contains("\"ErrorCatalog\"", pageStructuredJson, StringComparison.Ordinal);
            Assert.Contains("\"ResponseHeaders\"", pageStructuredJson, StringComparison.Ordinal);
            Assert.Contains("\"SuccessResponseSchema\"", pageStructuredJson, StringComparison.Ordinal);
            Assert.Contains("\"ErrorResponseSchema\"", pageStructuredJson, StringComparison.Ordinal);
            Assert.Contains("\"RequestBodyFields\"", pageStructuredJson, StringComparison.Ordinal);
            Assert.Contains("\"SuccessResponseFields\"", pageStructuredJson, StringComparison.Ordinal);
            Assert.Contains("\"ErrorResponseFields\"", pageStructuredJson, StringComparison.Ordinal);
            Assert.Contains("Example page", pageStructuredJson, StringComparison.Ordinal);
            Assert.Contains("OG Example", pageStructuredJson, StringComparison.Ordinal);

            string structuredPagesJsonl = File.ReadAllText(result.StructuredJsonPagesJsonlPath!);
            Assert.Contains("\"StructuredJson\"", structuredPagesJsonl, StringComparison.Ordinal);
            Assert.Equal(1, result.Summary.StructuredAuthenticatedApiEndpointCount);
            Assert.Equal(1, result.Summary.StructuredRateLimitedApiEndpointCount);
            Assert.Equal(1, result.Summary.StructuredApiErrorResponseCount);
        } finally {
            server.Stop();
            server.Close();
            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CrawlAsync_DatasetScenario_EnablesMarkdownAndStructuredJsonWithoutExplicitFlags() {
        Dictionary<string, string> responses = new() {
            ["/"] = """
            <html>
              <head><title>Dataset</title></head>
              <body>
                <main>
                  <h1>Dataset page</h1>
                  <p>Enough content for reader-mode dataset defaults to keep a useful offline representation.</p>
                </main>
              </body>
            </html>
            """
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        string outputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl, new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Scenario = HtmlCrawlScenario.Dataset,
                OutputPath = outputPath
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.Equal(HtmlCrawlScenario.Dataset, page.AppliedScenario);
            Assert.False(string.IsNullOrWhiteSpace(page.Markdown));
            Assert.NotNull(page.StructuredJson);
            Assert.Contains("Dataset page", page.StructuredJson!.Document.Text, StringComparison.Ordinal);
            Assert.Contains("# Dataset page", page.StructuredJson.Document.Markdown, StringComparison.Ordinal);
        } finally {
            server.Stop();
            server.Close();
            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CrawlAsync_DocsPreset_PopulatesFlattenedPresetFields() {
        Dictionary<string, string> responses = new() {
            ["/docs/getting-started"] = """
            <html>
              <head>
                <title>Getting Started | Example Docs</title>
                <meta name="description" content="Install and run the example tool.">
              </head>
              <body>
                <header>
                  <nav aria-label="Primary">
                    <a href="/docs">Docs</a>
                    <a href="/docs/getting-started">Getting Started</a>
                    <a href="/docs/reference">Reference</a>
                    <a href="/blog">Blog</a>
                  </nav>
                </header>
                <nav class="breadcrumbs" aria-label="Breadcrumb">
                  <a href="/docs">Docs</a>
                  <a href="/docs/getting-started">Getting Started</a>
                </nav>
                <main>
                  <h1>Getting Started</h1>
                  <aside class="callout tip">
                    <strong>Tip</strong>
                    <p>Use the dataset scenario for AI-ready exports.</p>
                  </aside>
                  <p>Run the command below to install the package.</p>
                  <a class="button primary" href="/docs/install">Install package</a>
                  <h2>POST /v1/widgets</h2>
                  <p>Create a widget from your integration.</p>
                  <p>Send your API key in the X-API-Key header. This endpoint is limited to 60 requests per minute and returns 429 with Retry-After when throttled.</p>
                  <h3>Headers</h3>
                  <table class="parameters">
                    <tr><th>Name</th><th>Type</th><th>Required</th><th>Example</th><th>Description</th></tr>
                    <tr><td>X-API-Key</td><td>string</td><td>Yes</td><td>sk_live_123</td><td>API key used for authentication.</td></tr>
                  </table>
                  <h3>Body parameters</h3>
                  <table class="parameters">
                    <tr><th>Name</th><th>Type</th><th>Required</th><th>Nullable</th><th>Format</th><th>Enum</th><th>Example</th><th>Pattern</th><th>Description</th></tr>
                    <tr><td>name</td><td>string</td><td>Yes</td><td>No</td><td>slug</td><td>alpha,beta</td><td>alpha</td><td>^[a-z]+$</td><td>Widget slug. One of: alpha, beta.</td></tr>
                  </table>
                  <pre><code class="language-http">POST /v1/widgets HTTP/1.1
                  Content-Type: application/json

                  { "name": "Widget" }</code></pre>
                  <h3>Response 201</h3>
                  <pre><code class="language-json">{
                  "id": "wid_456",
                  "name": "Widget",
                  "meta": {
                    "createdAt": "2026-03-15T10:00:00Z"
                  },
                  "tags": ["alpha", "beta"]
                  }</code></pre>
                  <h3>Error 429</h3>
                  <pre><code class="language-http">HTTP/1.1 429 Too Many Requests
                  Retry-After: 60
                  X-RateLimit-Remaining: 0
                  Content-Type: application/json

                  {
                  "error": "rate_limited",
                  "details": {
                    "retryAfterSeconds": 60
                  }
                  }</code></pre>
                  <h2>Install</h2>
                  <pre><code>dotnet add package HtmlTinkerX</code></pre>
                  <details>
                    <summary>Does this work offline?</summary>
                    <p>Yes. The crawl output stays local.</p>
                  </details>
                </main>
              </body>
            </html>
            """
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        string outputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl + "docs/getting-started", new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Selector = "main",
                IncludeStructuredJson = true,
                StructuredJsonPreset = HtmlCrawlStructuredJsonPreset.Docs,
                OutputPath = outputPath
            });

            HtmlCrawlPage page = Assert.Single(result.Pages);
            Assert.NotNull(page.StructuredJson);
            Assert.Equal(HtmlCrawlStructuredJsonPreset.Docs, page.StructuredJson!.ResolvedPreset);
            Assert.Equal(4, page.StructuredJson.CodeBlocks.Count);
            Assert.Equal(4, page.StructuredJson.CodeSamples.Count);
            Assert.Single(page.StructuredJson.Breadcrumbs);
            Assert.Single(page.StructuredJson.FaqItems);
            Assert.Single(page.StructuredJson.Callouts);
            Assert.Single(page.StructuredJson.PrimaryActions);
            Assert.Single(page.StructuredJson.ApiEndpoints);
            Assert.Equal("Getting Started | Example Docs", page.StructuredJson.Extracted["title"]);
            Assert.Equal("Getting Started", page.StructuredJson.Extracted["mainHeading"]);
            Assert.Equal(4, page.StructuredJson.Extracted["codeBlockCount"]);
            Assert.Equal(4, page.StructuredJson.Extracted["codeSampleCount"]);
            Assert.Equal(1, page.StructuredJson.Extracted["apiEndpointCount"]);
            List<object?> navigationLinks = Assert.IsType<List<object?>>(page.StructuredJson.Extracted["navigationLinks"]);
            Assert.Contains("Reference", navigationLinks);
            IList<string> breadcrumbs = Assert.IsAssignableFrom<IList<string>>(page.StructuredJson.Extracted["breadcrumbs"]);
            Assert.Equal(new object?[] { "Docs", "Getting Started" }, breadcrumbs);
            IList<HtmlCrawlStructuredCodeSample> codeSamples = Assert.IsAssignableFrom<IList<HtmlCrawlStructuredCodeSample>>(page.StructuredJson.Extracted["codeSamples"]);
            Assert.Equal(4, codeSamples.Count);
            IList<HtmlCrawlStructuredApiEndpoint> apiEndpoints = Assert.IsAssignableFrom<IList<HtmlCrawlStructuredApiEndpoint>>(page.StructuredJson.Extracted["apiEndpoints"]);
            Assert.Single(apiEndpoints);
            Assert.Equal("postWidgets", apiEndpoints[0].OperationId);
            Assert.Equal("widgets", apiEndpoints[0].Resource);
            Assert.Contains("widgets", apiEndpoints[0].Tags, StringComparer.OrdinalIgnoreCase);
            Assert.Equal(2, apiEndpoints[0].Parameters.Count);
            Assert.Single(apiEndpoints[0].HeaderParameters);
            Assert.Single(apiEndpoints[0].BodyParameters);
            Assert.Equal("string", apiEndpoints[0].RequestBodySchema["name"]);
            Assert.Equal("slug", apiEndpoints[0].BodyParameters[0].Format);
            Assert.False(apiEndpoints[0].BodyParameters[0].Nullable ?? true);
            Assert.Equal("alpha", apiEndpoints[0].BodyParameters[0].ExampleValue);
            Assert.Equal("^[a-z]+$", apiEndpoints[0].BodyParameters[0].Pattern);
            Assert.Contains("alpha", apiEndpoints[0].BodyParameters[0].EnumValues, StringComparer.OrdinalIgnoreCase);
            Assert.Single(apiEndpoints[0].RequestBodyFields);
            Assert.True(apiEndpoints[0].Authentication.Required ?? false);
            Assert.Contains("api-key", apiEndpoints[0].Authentication.Schemes, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("X-API-Key", apiEndpoints[0].Authentication.Headers, StringComparer.OrdinalIgnoreCase);
            Assert.True(apiEndpoints[0].RateLimit.Mentioned);
            Assert.Equal(429, apiEndpoints[0].RateLimit.StatusCode);
            Assert.Equal("60 requests per minute", apiEndpoints[0].RateLimit.Limit);
            Assert.Single(apiEndpoints[0].RequestExamples);
            Assert.Contains(apiEndpoints[0].RequestHeaders, header => string.Equals(header.Name, "Content-Type", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(apiEndpoints[0].RequestHeaders, header => string.Equals(header.Name, "X-API-Key", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(2, apiEndpoints[0].ResponseExamples.Count);
            Assert.Single(apiEndpoints[0].ErrorResponses);
            Assert.Single(apiEndpoints[0].ErrorCatalog);
            Assert.Contains(apiEndpoints[0].ResponseHeaders, header => string.Equals(header.Name, "Retry-After", StringComparison.OrdinalIgnoreCase));
            Assert.Equal("string", apiEndpoints[0].SuccessResponseSchema["id"]);
            Assert.Equal("string", apiEndpoints[0].SuccessResponseSchema["name"]);
            Assert.Equal("object", apiEndpoints[0].SuccessResponseSchema["meta"]);
            Assert.Equal("array", apiEndpoints[0].SuccessResponseSchema["tags"]);
            Assert.Equal("string", apiEndpoints[0].ErrorResponseSchema["error"]);
            Assert.Equal("object", apiEndpoints[0].ErrorResponseSchema["details"]);
            Assert.Contains(apiEndpoints[0].SuccessResponseFields, field => string.Equals(field.Path, "meta", StringComparison.Ordinal) && string.Equals(field.Kind, "object", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(apiEndpoints[0].SuccessResponseFields, field => string.Equals(field.Path, "tags", StringComparison.Ordinal) && string.Equals(field.Kind, "array", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(apiEndpoints[0].ErrorResponseFields, field => string.Equals(field.Path, "details", StringComparison.Ordinal) && string.Equals(field.Kind, "object", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(true, page.StructuredJson.Extracted["authRequired"]);
            Assert.Equal("postWidgets", page.StructuredJson.Extracted["operationId"]);
            Assert.Equal("widgets", page.StructuredJson.Extracted["resource"]);
            IList<string> extractedTags = Assert.IsAssignableFrom<IList<string>>(page.StructuredJson.Extracted["tags"]);
            Assert.Contains("widgets", extractedTags, StringComparer.OrdinalIgnoreCase);
            IList<string> apiTags = Assert.IsAssignableFrom<IList<string>>(page.StructuredJson.Extracted["apiTags"]);
            Assert.Contains("widgets", apiTags, StringComparer.OrdinalIgnoreCase);
            IList<string> apiResources = Assert.IsAssignableFrom<IList<string>>(page.StructuredJson.Extracted["apiResources"]);
            Assert.Contains("widgets", apiResources, StringComparer.OrdinalIgnoreCase);
            IList<string> operationIds = Assert.IsAssignableFrom<IList<string>>(page.StructuredJson.Extracted["operationIds"]);
            Assert.Contains("postWidgets", operationIds, StringComparer.OrdinalIgnoreCase);
            HtmlCrawlStructuredApiCatalog apiCatalog = Assert.IsType<HtmlCrawlStructuredApiCatalog>(page.StructuredJson.Extracted["apiCatalog"]);
            Assert.Equal(1, apiCatalog.OperationCount);
            HtmlCrawlStructuredOpenApiLike openApiLike = Assert.IsType<HtmlCrawlStructuredOpenApiLike>(page.StructuredJson.Extracted["openApiLike"]);
            Assert.True(openApiLike.Paths.ContainsKey("/v1/widgets"));
            IDictionary<string, HtmlCrawlStructuredOpenApiPathItem> openApiPaths = Assert.IsAssignableFrom<IDictionary<string, HtmlCrawlStructuredOpenApiPathItem>>(page.StructuredJson.Extracted["openApiPaths"]);
            Assert.True(openApiPaths.ContainsKey("/v1/widgets"));
            IList<string> openApiServers = Assert.IsAssignableFrom<IList<string>>(page.StructuredJson.Extracted["openApiServers"]);
            Assert.Contains(rootUrl.TrimEnd('/'), openApiServers, StringComparer.OrdinalIgnoreCase);
            IList<string> authenticationSchemes = Assert.IsAssignableFrom<IList<string>>(page.StructuredJson.Extracted["authenticationSchemes"]);
            Assert.Contains("api-key", authenticationSchemes, StringComparer.OrdinalIgnoreCase);
            IList<string> authenticationHeaders = Assert.IsAssignableFrom<IList<string>>(page.StructuredJson.Extracted["authenticationHeaders"]);
            Assert.Contains("X-API-Key", authenticationHeaders, StringComparer.OrdinalIgnoreCase);
            HtmlCrawlStructuredApiRateLimit rateLimit = Assert.IsType<HtmlCrawlStructuredApiRateLimit>(page.StructuredJson.Extracted["rateLimit"]);
            Assert.Equal(429, rateLimit.StatusCode);
            IList<string> rateLimitHeaders = Assert.IsAssignableFrom<IList<string>>(page.StructuredJson.Extracted["rateLimitHeaders"]);
            Assert.Contains("Retry-After", rateLimitHeaders, StringComparer.OrdinalIgnoreCase);
            Assert.Equal(429, page.StructuredJson.Extracted["rateLimitStatusCode"]);
            IList<HtmlCrawlStructuredRequestExample> requestExamples = Assert.IsAssignableFrom<IList<HtmlCrawlStructuredRequestExample>>(page.StructuredJson.Extracted["requestExamples"]);
            Assert.Single(requestExamples);
            Assert.Equal(1, page.StructuredJson.Extracted["requestExampleCount"]);
            IList<HtmlCrawlStructuredHttpHeader> requestHeaders = Assert.IsAssignableFrom<IList<HtmlCrawlStructuredHttpHeader>>(page.StructuredJson.Extracted["requestHeaders"]);
            Assert.Contains(requestHeaders, header => string.Equals(header.Name, "X-API-Key", StringComparison.OrdinalIgnoreCase));
            IList<HtmlCrawlStructuredHttpHeader> responseHeaders = Assert.IsAssignableFrom<IList<HtmlCrawlStructuredHttpHeader>>(page.StructuredJson.Extracted["responseHeaders"]);
            Assert.Contains(responseHeaders, header => string.Equals(header.Name, "Retry-After", StringComparison.OrdinalIgnoreCase));
            IList<HtmlCrawlStructuredResponseExample> errorResponses = Assert.IsAssignableFrom<IList<HtmlCrawlStructuredResponseExample>>(page.StructuredJson.Extracted["errorResponses"]);
            Assert.Single(errorResponses);
            Assert.Equal(1, page.StructuredJson.Extracted["errorResponseCount"]);
            IList<HtmlCrawlStructuredApiError> errorCatalog = Assert.IsAssignableFrom<IList<HtmlCrawlStructuredApiError>>(page.StructuredJson.Extracted["errorCatalog"]);
            Assert.Single(errorCatalog);
            Assert.Equal(1, page.StructuredJson.Extracted["errorCatalogCount"]);
            IDictionary<string, string?> successResponseSchema = Assert.IsAssignableFrom<IDictionary<string, string?>>(page.StructuredJson.Extracted["successResponseSchema"]);
            Assert.Equal("string", successResponseSchema["id"]);
            IDictionary<string, string?> errorResponseSchema = Assert.IsAssignableFrom<IDictionary<string, string?>>(page.StructuredJson.Extracted["errorResponseSchema"]);
            Assert.Equal("string", errorResponseSchema["error"]);
            IList<HtmlCrawlStructuredField> requestBodyFields = Assert.IsAssignableFrom<IList<HtmlCrawlStructuredField>>(page.StructuredJson.Extracted["requestBodyFields"]);
            Assert.Single(requestBodyFields);
            IList<HtmlCrawlStructuredField> successResponseFields = Assert.IsAssignableFrom<IList<HtmlCrawlStructuredField>>(page.StructuredJson.Extracted["successResponseFields"]);
            Assert.Contains(successResponseFields, field => string.Equals(field.Path, "meta", StringComparison.Ordinal) && string.Equals(field.Kind, "object", StringComparison.OrdinalIgnoreCase));
            IList<HtmlCrawlStructuredField> errorResponseFields = Assert.IsAssignableFrom<IList<HtmlCrawlStructuredField>>(page.StructuredJson.Extracted["errorResponseFields"]);
            Assert.Contains(errorResponseFields, field => string.Equals(field.Path, "details", StringComparison.Ordinal) && string.Equals(field.Kind, "object", StringComparison.OrdinalIgnoreCase));
            IList<HtmlCrawlStructuredFaqItem> faqItems = Assert.IsAssignableFrom<IList<HtmlCrawlStructuredFaqItem>>(page.StructuredJson.Extracted["faqItems"]);
            Assert.Single(faqItems);
            Assert.Equal(1, page.StructuredJson.Extracted["calloutCount"]);
            IList<HtmlCrawlStructuredCallout> callouts = Assert.IsAssignableFrom<IList<HtmlCrawlStructuredCallout>>(page.StructuredJson.Extracted["callouts"]);
            Assert.Single(callouts);
            IList<HtmlCrawlStructuredPrimaryAction> primaryActions = Assert.IsAssignableFrom<IList<HtmlCrawlStructuredPrimaryAction>>(page.StructuredJson.Extracted["primaryActions"]);
            Assert.Single(primaryActions);
        } finally {
            server.Stop();
            server.Close();
            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CrawlAsync_IncludeStructuredJson_BuildsMergedOpenApiLikeArtifactAcrossPages() {
        Dictionary<string, string> responses = new() {
            ["/docs/create-widget"] = """
            <html>
              <head>
                <title>Widget API</title>
                <meta name="description" content="Widget API reference">
              </head>
              <body>
                <main>
                  <h1>Widget API</h1>
                  <p><a href="/docs/list-widgets">List widgets</a></p>
                  <h2>POST /v1/widgets</h2>
                  <p>Create a widget.</p>
                  <p>Authenticate with your API key using the X-API-Key header.</p>
                  <h3>Headers</h3>
                  <table class="parameters">
                    <tr><th>Name</th><th>Type</th><th>Required</th><th>Example</th><th>Description</th></tr>
                    <tr><td>X-API-Key</td><td>string</td><td>Yes</td><td>sk_live_123</td><td>API key used for authentication.</td></tr>
                  </table>
                  <h3>Body parameters</h3>
                  <table class="parameters">
                    <tr><th>Name</th><th>Type</th><th>Required</th><th>Description</th></tr>
                    <tr><td>name</td><td>string</td><td>Yes</td><td>Name of the widget.</td></tr>
                  </table>
                  <pre><code class="language-http">POST /v1/widgets HTTP/1.1
                  Content-Type: application/json

                  { "name": "Alpha" }</code></pre>
                  <h3>Response 201</h3>
                  <pre><code class="language-json">{
                  "id": "wid_123",
                  "name": "Alpha"
                  }</code></pre>
                  <h3>Error 429</h3>
                  <pre><code class="language-http">HTTP/1.1 429 Too Many Requests
                  Retry-After: 60
                  Content-Type: application/json

                  {
                  "error": "rate_limited"
                  }</code></pre>
                </main>
              </body>
            </html>
            """,
            ["/docs/list-widgets"] = """
            <html>
              <head>
                <title>Widget API</title>
              </head>
              <body>
                <main>
                  <h1>Widget API</h1>
                  <h2>GET /v1/widgets</h2>
                  <p>List widgets.</p>
                  <p>Authenticate with your API key using the X-API-Key header.</p>
                  <h3>Headers</h3>
                  <table class="parameters">
                    <tr><th>Name</th><th>Type</th><th>Required</th><th>Description</th></tr>
                    <tr><td>X-API-Key</td><td>string</td><td>Yes</td><td>API key used for authentication.</td></tr>
                  </table>
                  <pre><code class="language-http">GET /v1/widgets HTTP/1.1
                  Accept: application/json</code></pre>
                  <h3>Response 200</h3>
                  <pre><code class="language-json">[
                  { "id": "wid_123", "name": "Alpha" }
                  ]</code></pre>
                </main>
              </body>
            </html>
            """
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        string outputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl + "docs/create-widget", new HtmlCrawlOptions {
                MaxDepth = 1,
                MaxPages = 5,
                Selector = "main",
                IncludeStructuredJson = true,
                OutputPath = outputPath
            });

            Assert.Equal(2, result.Pages.Count);
            Assert.NotNull(result.OpenApiLike);
            Assert.Single(result.OpenApiLike.Paths);
            Assert.True(result.OpenApiLike.Paths.ContainsKey("/v1/widgets"));
            HtmlCrawlStructuredOpenApiPathItem pathItem = result.OpenApiLike.Paths["/v1/widgets"];
            Assert.True(pathItem.Operations.ContainsKey("post"));
            Assert.True(pathItem.Operations.ContainsKey("get"));
            Assert.Equal("postWidgets", pathItem.Operations["post"].OperationId);
            Assert.Equal("getWidgets", pathItem.Operations["get"].OperationId);
            Assert.True(pathItem.Operations["post"].StrictOpenApiEligible);
            Assert.True(pathItem.Operations["get"].StrictOpenApiEligible);
            Assert.True(pathItem.Operations["post"].StrictOpenApiScore >= result.OpenApiLike.StrictOpenApiPromotionThreshold);
            Assert.True(pathItem.Operations["get"].StrictOpenApiScore >= result.OpenApiLike.StrictOpenApiPromotionThreshold);
            Assert.Contains(rootUrl + "docs/create-widget", pathItem.Operations["post"].Provenance.PageUrls, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Heading", pathItem.Operations["post"].Provenance.SourceKinds, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("CodeSample", pathItem.Operations["post"].Provenance.SourceKinds, StringComparer.OrdinalIgnoreCase);
            Assert.NotEmpty(pathItem.Operations["post"].Provenance.Entries);
            Assert.Equal(pathItem.Operations["post"].AuthenticationRef, pathItem.Operations["get"].AuthenticationRef);
            Assert.Contains("widgets", result.OpenApiLike.Resources, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("widgets", result.OpenApiLike.Tags, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(rootUrl.TrimEnd('/'), result.OpenApiLike.Servers, StringComparer.OrdinalIgnoreCase);
            Assert.Equal(2, result.OpenApiLike.StrictOpenApiEligibleOperationCount);
            Assert.Equal(0, result.OpenApiLike.StrictOpenApiSkippedOperationCount);
            Assert.True(result.OpenApiLike.StrictOpenApiAverageScore > 0);
            Assert.Equal(1, result.Summary.StructuredApiPathCount);
            Assert.Equal(1, result.Summary.StructuredApiResourceCount);
            Assert.Equal(2, result.Summary.StructuredOpenApiPromotedOperationCount);
            Assert.Equal(0, result.Summary.StructuredOpenApiSkippedOperationCount);
            Assert.True(result.Summary.StructuredOpenApiAveragePromotionScore > 0);
            Assert.True(result.Summary.StructuredApiSchemaComponentCount > 0);
            Assert.True(result.Summary.StructuredApiFieldSetComponentCount > 0);
            Assert.True(result.Summary.StructuredApiAuthProfileCount > 0);
            Assert.True(result.Summary.StructuredApiRateLimitProfileCount >= 0);
            Assert.True(result.Summary.StructuredApiParameterSetCount > 0);
            Assert.True(result.Summary.StructuredApiHeaderSetCount > 0);
            Assert.True(result.Summary.StructuredApiExampleSetCount > 0);
            Assert.True(result.Summary.StructuredApiErrorCatalogComponentCount > 0);
            Assert.NotEmpty(result.OpenApiLike.Components.Schemas);
            Assert.NotEmpty(result.OpenApiLike.Components.FieldSets);
            Assert.NotEmpty(result.OpenApiLike.Components.AuthProfiles);
            Assert.NotEmpty(result.OpenApiLike.Components.ParameterSets);
            Assert.NotEmpty(result.OpenApiLike.Components.RequestHeaderSets);
            Assert.NotEmpty(result.OpenApiLike.Components.ResponseHeaderSets);
            Assert.NotEmpty(result.OpenApiLike.Components.RequestExampleSets);
            Assert.NotEmpty(result.OpenApiLike.Components.ResponseExampleSets);
            Assert.NotEmpty(result.OpenApiLike.Components.ErrorCatalogs);

            Assert.False(string.IsNullOrWhiteSpace(result.OpenApiLikePath));
            Assert.True(File.Exists(result.OpenApiLikePath!));
            string openApiLikeJson = File.ReadAllText(result.OpenApiLikePath!);
            Assert.Contains("\"/v1/widgets\"", openApiLikeJson, StringComparison.Ordinal);
            Assert.Contains("\"post\"", openApiLikeJson, StringComparison.Ordinal);
            Assert.Contains("\"get\"", openApiLikeJson, StringComparison.Ordinal);

            Assert.False(string.IsNullOrWhiteSpace(result.OpenApiPath));
            Assert.True(File.Exists(result.OpenApiPath!));
            Assert.Equal("3.1.0", Assert.IsType<string>(result.OpenApiDocument["openapi"]));
            Assert.True(result.OpenApiDocument.ContainsKey("components"));
            Assert.True(result.OpenApiDocument.ContainsKey("x-htmltinkerx-promotion"));
            IDictionary<string, object?> strictPaths = Assert.IsAssignableFrom<IDictionary<string, object?>>(result.OpenApiDocument["paths"]);
            IDictionary<string, object?> strictWidgetsPath = Assert.IsAssignableFrom<IDictionary<string, object?>>(strictPaths["/v1/widgets"]);
            IDictionary<string, object?> strictPostOperation = Assert.IsAssignableFrom<IDictionary<string, object?>>(strictWidgetsPath["post"]);
            Assert.True(strictPostOperation.ContainsKey("requestBody"));
            List<object> strictParameters = Assert.IsAssignableFrom<List<object>>(strictPostOperation["parameters"]);
            Assert.DoesNotContain(strictParameters, parameter =>
                parameter is IDictionary<string, object?> parameterDictionary
                && string.Equals(parameterDictionary["name"] as string, "name", StringComparison.Ordinal)
                && string.Equals(parameterDictionary["in"] as string, "query", StringComparison.OrdinalIgnoreCase));
            string openApiJson = File.ReadAllText(result.OpenApiPath!);
            Assert.Contains("\"openapi\": \"3.1.0\"", openApiJson, StringComparison.Ordinal);
            Assert.Contains("\"/v1/widgets\"", openApiJson, StringComparison.Ordinal);
            Assert.Contains("\"securitySchemes\"", openApiJson, StringComparison.Ordinal);
            Assert.Contains("\"components\"", openApiJson, StringComparison.Ordinal);
            Assert.Contains("\"x-htmltinkerx-provenance\"", openApiJson, StringComparison.Ordinal);
            Assert.Contains("\"x-htmltinkerx-sourcePages\"", openApiJson, StringComparison.Ordinal);
            Assert.Contains("\"x-htmltinkerx-schemaProvenance\"", openApiJson, StringComparison.Ordinal);
            Assert.Contains("\"x-htmltinkerx-fieldProvenance\"", openApiJson, StringComparison.Ordinal);
            Assert.Contains("\"x-htmltinkerx-confidence\"", openApiJson, StringComparison.Ordinal);
            Assert.Contains("\"x-htmltinkerx-evidenceCount\"", openApiJson, StringComparison.Ordinal);
            Assert.Contains("\"x-htmltinkerx-confidenceSummary\"", openApiJson, StringComparison.Ordinal);

            Assert.False(string.IsNullOrWhiteSpace(result.IndexHtmlPath));
            string indexHtml = File.ReadAllText(result.IndexHtmlPath!);
            Assert.Contains("OpenAPI JSON", indexHtml, StringComparison.OrdinalIgnoreCase);
        } finally {
            server.Stop();
            server.Close();
            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
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
            server.Stop();
            server.Close();
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
            server.Stop();
            server.Close();
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
            server.Stop();
            server.Close();
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
            server.Stop();
            server.Close();
            if (Directory.Exists(outputPath)) {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }
}
