using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HtmlTinkerX;

public static partial class HtmlCrawler {
    private static JsonSerializerOptions CreateSnapshotJsonOptions() {
        JsonSerializerOptions options = CreateJsonOptions();
        options.Converters.Add(new CrawlPageSnapshotConverter(new JsonSerializerOptions(options)));
        return options;
    }

    // Keep the cache internal and preserve the ordinary page schema on serializers
    // used by .NET Framework as well as modern .NET. Only saved crawler snapshots
    // use this converter; per-page exports do not contain the original cached body.
    private sealed class CrawlPageSnapshotConverter(JsonSerializerOptions pageOptions) : JsonConverter<HtmlCrawlPage> {
        public override HtmlCrawlPage Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            HtmlCrawlPage page = JsonSerializer.Deserialize<HtmlCrawlPage>(document.RootElement.GetRawText(), pageOptions)
                ?? throw new JsonException("Invalid crawl page record.");
            if (document.RootElement.TryGetProperty("HttpCache", out JsonElement cache) && cache.ValueKind != JsonValueKind.Null) {
                page.HttpCache = JsonSerializer.Deserialize<HtmlCrawlHttpCacheEntry>(cache.GetRawText(), pageOptions);
            }
            return page;
        }

        public override void Write(Utf8JsonWriter writer, HtmlCrawlPage page, JsonSerializerOptions options) {
            using PageContentLease content = new(page);
            if (page.HttpCache == null) {
                JsonSerializer.Serialize(writer, page, pageOptions);
                return;
            }
            using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(page, pageOptions));
            writer.WriteStartObject();
            foreach (JsonProperty property in document.RootElement.EnumerateObject()) property.WriteTo(writer);
            writer.WritePropertyName("HttpCache");
            JsonSerializer.Serialize(writer, page.HttpCache, pageOptions);
            writer.WriteEndObject();
        }
    }
}
