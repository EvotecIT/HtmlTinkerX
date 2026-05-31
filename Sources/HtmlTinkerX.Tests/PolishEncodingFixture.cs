using System;
using System.Net.Http;
using System.Text;

namespace HtmlTinkerX.Tests;

internal static class PolishEncodingFixture {
    private const string Html = @"<!doctype html>
<html>
<head>
    <meta http-equiv=""Content-Type"" content=""text/html; charset=iso-8859-2"">
    <title>Polish table encoding fixture</title>
</head>
<body>
    <table><tr><td>Ignored 0</td></tr></table>
    <table><tr><td>Ignored 1</td></tr></table>
    <table><tr><td>Ignored 2</td></tr></table>
    <table><tr><td>Ignored 3</td></tr></table>
    <table><tr><td>Ignored 4</td></tr></table>
    <table><tr><td>Ignored 5</td></tr></table>
    <table><tr><td>Ignored 6</td></tr></table>
    <table><tr><td>Ignored 7</td></tr></table>
    <table><tr><td>Ignored 8</td></tr></table>
    <table><tr><td>Ignored 9</td></tr></table>
    <table><tr><td>Ignored 10</td></tr></table>
    <table><tr><td>Ignored 11</td></tr></table>
    <table>
        <tr><td>Komórka a1</td><td>Komórka a2</td></tr>
        <tr><td>Komórka a3</td><td>Komórka a4</td></tr>
    </table>
</body>
</html>";

    public static TestServerFixture CreateServer() {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var bytes = Encoding.GetEncoding("iso-8859-2").GetBytes(Html);
        return TestServerCompat.CreateTestServer(async context => {
            context.Response.ContentType = "text/html; charset=iso-8859-2";
            await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
        }, "/polish", "GET");
    }

    public static HttpClient CreateClient(TestServerFixture server) {
        var client = server.CreateClient();
        client.BaseAddress = new Uri("http://localhost");
        return client;
    }
}
