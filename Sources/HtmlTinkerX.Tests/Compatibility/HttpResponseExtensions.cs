using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace HtmlTinkerX.Tests
{
#if FRAMEWORK
    internal static class HttpResponseExtensions
    {
        public static Task WriteAsync(this HttpResponse response, string text)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(text);
            return response.Body.WriteAsync(bytes, 0, bytes.Length);
        }
    }
#endif
}