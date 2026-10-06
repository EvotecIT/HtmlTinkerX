using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace HtmlTinkerX;

public static partial class HtmlBrowserlessExtraction {
    // Reuse the parser's selected record arrays. Their contents already have item shapes;
    // walking only the envelope keeps ordinary record growth outside its node budget.
    private static string? CaptureResponseShape(JsonElement root, HashSet<string> recordArrays, out string? error) {
        try {
            int nodes = 0;
            string shape = JsonSerializer.Serialize(DescribeResponseShape(root, "$", recordArrays, 0, ref nodes), ShapeJsonOptions);
            error = null;
            return shape;
        } catch (InvalidDataException exception) {
            error = exception.Message;
            return null;
        } catch (JsonException exception) {
            // Metadata failure must not enter the payload parser's malformed-JSON fallback.
            error = "Response shape serialization failed: " + exception.Message;
            return null;
        }
    }

    private static object DescribeResponseShape(JsonElement value, string path,
        HashSet<string> recordArrays, int depth, ref int nodes) {
        if (++nodes > MaximumShapeNodes || depth > MaximumShapeDepth) {
            throw new InvalidDataException($"Response envelope shape exceeds {MaximumShapeNodes} nodes or depth {MaximumShapeDepth}.");
        }
        switch (value.ValueKind) {
            case JsonValueKind.Object:
                SortedDictionary<string, object> fields = new(StringComparer.Ordinal);
                foreach (JsonProperty field in value.EnumerateObject()) {
                    fields[field.Name] = DescribeResponseShape(field.Value, path + "." + field.Name, recordArrays, depth + 1, ref nodes);
                }
                return new { kind = "object", fields };
            case JsonValueKind.Array:
                SortedSet<string> elements = new(StringComparer.Ordinal);
                int index = 0;
                foreach (JsonElement element in value.EnumerateArray()) {
                    elements.Add(recordArrays.Contains(path) && IsRecordLike(element) ? "record-item"
                        : JsonSerializer.Serialize(DescribeResponseShape(element, path + "[" + index + "]", recordArrays, depth + 1, ref nodes), ShapeJsonOptions));
                    index++;
                }
                return new { kind = "array", elements };
            case JsonValueKind.String: return "string";
            case JsonValueKind.Number: return "number";
            case JsonValueKind.True:
            case JsonValueKind.False: return "boolean";
            default: return "null";
        }
    }
}
