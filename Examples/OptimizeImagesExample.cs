using HtmlTinkerX;

const string html = "<html><body><img src=\"https://via.placeholder.com/1x1.png\" /></body></html>";

string optimized = await HtmlOptimizer.OptimizeHtmlAsync(html, cssDecodeEscapes: false, embedImages: true);

Console.WriteLine(optimized);