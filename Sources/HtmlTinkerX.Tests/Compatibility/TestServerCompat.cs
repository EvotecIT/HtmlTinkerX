using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace HtmlTinkerX.Tests
{
    internal static class TestServerCompat
    {
        public static TestServer CreateTestServer(Func<HttpContext, Task> handler, string path = "/", string method = "POST")
        {
#if FRAMEWORK
            var builder = new WebHostBuilder()
                .Configure(app =>
                {
                    app.Run(async context =>
                    {
                        if (context.Request.Path.Value == path && context.Request.Method == method)
                        {
                            await handler(context);
                        }
                        else
                        {
                            context.Response.StatusCode = 404;
                        }
                    });
                });
#else
            var builder = new WebHostBuilder()
                .ConfigureServices(s => s.AddRouting())
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        if (method == "GET")
                            endpoints.MapGet(path, handler);
                        else
                            endpoints.MapPost(path, handler);
                    });
                });
#endif
            return new TestServer(builder);
        }

        public static TestServer CreateFormTestServer()
        {
            return CreateTestServer(async context =>
            {
                var form = await context.Request.ReadFormAsync();
                string user = form["user"].ToString();
                string pass = form["pass"].ToString();
                await context.Response.WriteAsync($"{user}:{pass}");
            }, "/login");
        }

        public static TestServer CreateListTestServer()
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

        public static TestServer CreateFormParsingTestServer()
        {
            return CreateTestServer(async context =>
            {
                string html = System.IO.File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Documents", "sample_form.html"));
                await context.Response.WriteAsync(html);
            }, "/form", "GET");
        }

        public static TestServer CreateListParsingTestServer()
        {
            return CreateTestServer(async context =>
            {
                string html = System.IO.File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Documents", "sample_lists.html"));
                await context.Response.WriteAsync(html);
            }, "/lists", "GET");
        }
    }
}