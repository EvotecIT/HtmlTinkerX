using HtmlTinkerX;
using System;

public static class CustomizeHtmlSettingsExample {
    public static void Run() {
        string html = "<input type=\"checkbox\" checked=\"checked\" />";

        string optimized = HtmlOptimizer.OptimizeHtml(
            html,
            cssDecodeEscapes: false,
            removeOptionalTags: true,
            shortBooleanAttributes: true);

        Console.WriteLine(optimized);
    }
}
