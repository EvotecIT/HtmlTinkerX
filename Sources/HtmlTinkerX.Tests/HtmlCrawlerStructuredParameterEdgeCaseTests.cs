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
                    <tr><th>Name</th><th>Location</th><th>Type</th><th>Required</th><th>Description</th></tr>
                    <tr><td>user.name</td><td>body</td><td>string</td><td>Yes</td><td>Not nullable.</td></tr>
                    <tr><td>items[]</td><td>body</td><td>string</td><td>No</td><td>Non-nullable array-style property.</td></tr>
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
            Assert.Equal(2, endpoint.BodyParameters.Count);
            Assert.All(endpoint.BodyParameters, parameter => Assert.False(parameter.Nullable ?? true));
            Assert.All(endpoint.RequestBodyFields, field => Assert.Null(field.ParentPath));

            IDictionary<string, object?> paths = Assert.IsAssignableFrom<IDictionary<string, object?>>(result.OpenApiDocument["paths"]);
            IDictionary<string, object?> usersPath = Assert.IsAssignableFrom<IDictionary<string, object?>>(paths["/v1/users"]);
            IDictionary<string, object?> postOperation = Assert.IsAssignableFrom<IDictionary<string, object?>>(usersPath["post"]);
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
        } finally {
            DisposeListenerSafely(server);
        }
    }
}
