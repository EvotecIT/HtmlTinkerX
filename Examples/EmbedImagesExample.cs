// Sample demonstrating HtmlOptimizer.EmbedImagesAsDataUriAsync
using System.Net.Http;
using HtmlTinkerX;

string html = "<html><body><img src=\"https://example.com/image.png\" /></body></html>";
string result = await HtmlOptimizer.EmbedImagesAsDataUriAsync(html, client: new HttpClient());
Console.WriteLine(result);
