using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public partial class HtmlCrawlerStructuredJsonTests {
    [Fact]
    public async Task CrawlAsync_RequestBodyParameterTables_PreserveOrphanLiteralNamesAndNegativeNullability() {
        Dictionary<string, string> responses = new() {
            ["/docs/users/create"] = """
            <html>
              <body>
                <main>
                  <h1>Create user</h1>
                  <h2>POST /v1/users</h2>
                  <h3>Request body parameters</h3>
                  <table class="parameters">
                    <tr><th>Name</th><th>Location</th><th>Type</th><th>Required</th><th>Nullable</th><th>Enum</th><th>Description</th></tr>
                    <tr><td>user.name</td><td>body</td><td>string</td><td>Yes</td><td></td><td></td><td>Not nullable.</td></tr>
                    <tr><td>items[]</td><td>body</td><td>string</td><td>No</td><td></td><td></td><td>Non-nullable array-style property.</td></tr>
                    <tr><td>status</td><td>body</td><td>string</td><td>No</td><td>Yes</td><td>active, disabled</td><td>Optional account status.</td></tr>
                    <tr><td>mode</td><td>query</td><td>string</td><td>No</td><td>Yes</td><td>fast, safe</td><td>Optional processing mode.</td></tr>
                  </table>
                  <h3>Response 201</h3>
                  <pre><code class="language-json">{ "id": "usr_123" }</code></pre>
                </main>
              </body>
            </html>
            """
        };

        HttpListener server = StartServer(responses, out string rootUrl);
        try {
            HtmlCrawlResult result = await HtmlCrawler.CrawlAsync(rootUrl + "docs/users/create", new HtmlCrawlOptions {
                MaxDepth = 0,
                MaxPages = 1,
                Selector = "main",
                IncludeStructuredJson = true
            });

            HtmlCrawlStructuredApiEndpoint endpoint = Assert.Single(Assert.Single(result.Pages).StructuredJson!.ApiEndpoints);
            Assert.Equal(3, endpoint.BodyParameters.Count);
            Assert.Single(endpoint.QueryParameters);
            Assert.False(endpoint.BodyParameters[0].Nullable ?? true);
            Assert.False(endpoint.BodyParameters[1].Nullable ?? true);
            Assert.True(endpoint.BodyParameters[2].Nullable);
            Assert.All(endpoint.RequestBodyFields, field => Assert.Null(field.ParentPath));

            IDictionary<string, object?> paths = Assert.IsAssignableFrom<IDictionary<string, object?>>(result.OpenApiDocument["paths"]);
            IDictionary<string, object?> usersPath = Assert.IsAssignableFrom<IDictionary<string, object?>>(paths["/v1/users"]);
            IDictionary<string, object?> postOperation = Assert.IsAssignableFrom<IDictionary<string, object?>>(usersPath["post"]);
            List<object> strictParameters = Assert.IsAssignableFrom<List<object>>(postOperation["parameters"]);
            IDictionary<string, object?> modeParameter = Assert.IsAssignableFrom<IDictionary<string, object?>>(Assert.Single(strictParameters));
            IDictionary<string, object?> modeSchema = Assert.IsAssignableFrom<IDictionary<string, object?>>(modeParameter["schema"]);
            IList<object?> modeEnum = Assert.IsAssignableFrom<IList<object?>>(modeSchema["enum"]);
            Assert.Contains(modeEnum, value => value is null);
            IDictionary<string, object?> requestBody = Assert.IsAssignableFrom<IDictionary<string, object?>>(postOperation["requestBody"]);
            IDictionary<string, object?> content = Assert.IsAssignableFrom<IDictionary<string, object?>>(requestBody["content"]);
            IDictionary<string, object?> jsonContent = Assert.IsAssignableFrom<IDictionary<string, object?>>(content["application/json"]);
            IDictionary<string, object?> schema = Assert.IsAssignableFrom<IDictionary<string, object?>>(jsonContent["schema"]);
            string schemaReference = Assert.IsType<string>(schema["$ref"]);
            string schemaName = schemaReference.Substring(schemaReference.LastIndexOf('/') + 1);
            IDictionary<string, object?> components = Assert.IsAssignableFrom<IDictionary<string, object?>>(result.OpenApiDocument["components"]);
            IDictionary<string, object?> schemas = Assert.IsAssignableFrom<IDictionary<string, object?>>(components["schemas"]);
            IDictionary<string, object?> componentSchema = Assert.IsAssignableFrom<IDictionary<string, object?>>(schemas[schemaName]);
            IDictionary<string, object?> properties = Assert.IsAssignableFrom<IDictionary<string, object?>>(componentSchema["properties"]);
            Assert.Contains("user.name", properties.Keys, StringComparer.Ordinal);
            Assert.Contains("items[]", properties.Keys, StringComparer.Ordinal);
            IDictionary<string, object?> statusSchema = Assert.IsAssignableFrom<IDictionary<string, object?>>(properties["status"]);
            IList<object?> statusEnum = Assert.IsAssignableFrom<IList<object?>>(statusSchema["enum"]);
            Assert.Contains(statusEnum, value => value is null);
        } finally {
            DisposeListenerSafely(server);
        }
    }

    [Fact]
    public async Task CrawlAsync_AbsoluteApiTargets_PreserveApiOriginAsServer() {
        Dictionary<string, string> responses = new() {
            ["/docs/users/list"] = """
            <html>
              <body>
                <main>
                  <h1>List users</h1>
                  <h2>GET https://api.example.com/v1/users</h2>
                  <h3>Response 200</h3>
                  <pre><code class="language-json">[{ "id": "usr_123" }]</code></pre>
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

            HtmlCrawlStructuredApiEndpoint endpoint = Assert.Single(Assert.Single(result.Pages).StructuredJson!.ApiEndpoints);
            Assert.Contains("https://api.example.com", endpoint.Servers, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("https://api.example.com", result.OpenApiLike.Servers, StringComparer.OrdinalIgnoreCase);

            IList<Dictionary<string, object?>> servers = Assert.IsAssignableFrom<IList<Dictionary<string, object?>>>(result.OpenApiDocument["servers"]);
            Assert.Contains(servers, serverEntry =>
                string.Equals(serverEntry["url"] as string, "https://api.example.com", StringComparison.OrdinalIgnoreCase));
        } finally {
            DisposeListenerSafely(server);
        }
    }
}
