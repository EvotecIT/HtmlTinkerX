using HtmlTinkerX;
using System;

public static class CustomizeHtmlSettingsExample {
    public static void Run() {
        string html = "<input type=\"checkbox\" checked=\"checked\" />";

        var settings = HtmlOptimizer.CreateDefaultHtmlSettings();
        settings.RemoveOptionalTags = true;
        settings.ShortBooleanAttribute = true;
        settings.RemoveAttributeQuotes = false;

        string optimized = HtmlOptimizer.OptimizeHtml(html, settings);

        Console.WriteLine(optimized);
    }
}
