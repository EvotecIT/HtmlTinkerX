using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using System;
using System.Net.Http;
using System.Threading.Tasks;
#if !FRAMEWORK
using Microsoft.Extensions.Hosting;
#endif

namespace HtmlTinkerX.Tests
{
    internal sealed class TestServerFixture : IDisposable
    {
        private readonly TestServer _server;
#if !FRAMEWORK
        private readonly IHost? _host;

        public TestServerFixture(IHost host)
        {
            _host = host;
            _server = host.GetTestServer();
        }
#else
        public TestServerFixture(TestServer server)
        {
            _server = server;
        }
#endif

        public Uri BaseAddress => _server.BaseAddress;

        public HttpClient CreateClient()
        {
            return _server.CreateClient();
        }

        public void Dispose()
        {
            _server.Dispose();
#if !FRAMEWORK
            _host?.Dispose();
#endif
        }
    }

    internal static class TestServerCompat
    {
        public static TestServerFixture CreateTestServer(Func<HttpContext, Task> handler, string? path = "/", string? method = "POST")
        {
#if FRAMEWORK
            var builder = new WebHostBuilder()
                .Configure(app => ConfigureApplication(app, handler, path, method));
            return new TestServerFixture(new TestServer(builder));
#else
            IHost host = new HostBuilder()
                .ConfigureWebHost(webHost =>
                {
                    webHost.UseTestServer();
                    webHost.Configure(app => ConfigureApplication(app, handler, path, method));
                })
                .Build();
            host.Start();
            return new TestServerFixture(host);
#endif
        }

        private static void ConfigureApplication(IApplicationBuilder app, Func<HttpContext, Task> handler, string? path, string? method)
        {
            app.Run(async context =>
            {
                if (ShouldHandle(context, path, method))
                {
                    await handler(context);
                }
                else
                {
                    context.Response.StatusCode = 404;
                }
            });
        }

        private static bool ShouldHandle(HttpContext context, string? path, string? method)
        {
            bool pathMatches = path == null || string.Equals(context.Request.Path.Value, path, StringComparison.Ordinal);
            bool methodMatches = method == null || string.Equals(context.Request.Method, method, StringComparison.OrdinalIgnoreCase);
            return pathMatches && methodMatches;
        }

        public static TestServerFixture CreateFormTestServer()
        {
            return CreateTestServer(async context =>
            {
                var form = await context.Request.ReadFormAsync();
                string user = form["user"].ToString();
                string pass = form["pass"].ToString();
                await context.Response.WriteAsync($"{user}:{pass}");
            }, "/login");
        }

        public static TestServerFixture CreateListTestServer()
        {
            return CreateTestServer(async context =>
            {
                await context.Response.WriteAsync(@"<ul id='list'>
                    <li>Item 1</li>
                    <li>Item 2</li>
                    <li>Item 3</li>
                </ul>");
            }, "/list");
        }

        public static TestServerFixture CreateFormParsingTestServer()
        {
            return CreateTestServer(async context =>
            {
                string html = System.IO.File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Documents", "sample_form.html"));
                await context.Response.WriteAsync(html);
            }, "/form", "GET");
        }

        public static TestServerFixture CreateListParsingTestServer()
        {
            return CreateTestServer(async context =>
            {
                string html = System.IO.File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Documents", "sample_lists.html"));
                await context.Response.WriteAsync(html);
            }, "/lists", "GET");
        }
    }
}
