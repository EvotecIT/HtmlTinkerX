using Acornima;
using Acornima.Ast;
using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AcornimaNode = Acornima.Ast.Node;

namespace HtmlTinkerX;

/// <summary>
/// Type of inline React Flight instruction emitted by Next.js.
/// </summary>
public enum HtmlReactFlightPayloadKind {
    /// <summary>Unknown or unsupported payload kind.</summary>
    Unknown = -1,

    /// <summary>Bootstrap instruction that initializes the client buffer.</summary>
    Bootstrap = 0,

    /// <summary>UTF-8 text payload chunk.</summary>
    Data = 1,

    /// <summary>Serialized form-state payload.</summary>
    FormState = 2,

    /// <summary>Base64-encoded binary payload chunk.</summary>
    Binary = 3
}

/// <summary>
/// Parsed React Flight payloads and rows extracted from an HTML document.
/// </summary>
public sealed class HtmlReactFlightDocument {
    /// <summary>Inline payload instructions in document order.</summary>
    public List<HtmlReactFlightPayload> Payloads { get; } = new();

    /// <summary>Rows decoded from the concatenated React Flight stream.</summary>
    public List<HtmlReactFlightRow> Rows { get; } = new();

    /// <summary>Concatenated UTF-8 stream text when the stream can be represented as text.</summary>
    public string StreamText { get; set; } = string.Empty;
}

/// <summary>
/// Inline Next.js React Flight payload instruction.
/// </summary>
public sealed class HtmlReactFlightPayload {
    /// <summary>Zero-based payload instruction index.</summary>
    public int Index { get; set; }

    /// <summary>Zero-based script element index in the source document.</summary>
    public int ScriptIndex { get; set; }

    /// <summary>Zero-based instruction index within the script element.</summary>
    public int ScriptInstructionIndex { get; set; }

    /// <summary>Raw numeric instruction kind emitted by Next.js.</summary>
    public int KindCode { get; set; }

    /// <summary>Friendly payload kind.</summary>
    public HtmlReactFlightPayloadKind Kind { get; set; } = HtmlReactFlightPayloadKind.Unknown;

    /// <summary>Text payload for data chunks.</summary>
    public string? Text { get; set; }

    /// <summary>Base64 payload for binary chunks.</summary>
    public string? Base64 { get; set; }

    /// <summary>Decoded binary payload bytes.</summary>
    public byte[]? Bytes { get; set; }

    /// <summary>Serialized form state when the payload contains one.</summary>
    public string? FormStateJson { get; set; }

    /// <summary>JSON representation of the extracted push instruction.</summary>
    public string RawJson { get; set; } = string.Empty;

    /// <summary>Byte offset where this payload begins in the reconstructed stream.</summary>
    public int StreamOffset { get; set; }

    /// <summary>Number of stream bytes contributed by this payload.</summary>
    public int StreamLength { get; set; }
}

/// <summary>
/// Row decoded from a React Flight stream.
/// </summary>
public sealed class HtmlReactFlightRow {
    /// <summary>Zero-based row index.</summary>
    public int Index { get; set; }

    /// <summary>React Flight row id.</summary>
    public int Id { get; set; }

    /// <summary>Optional React Flight row tag.</summary>
    public string? Tag { get; set; }

    /// <summary>Human-readable description of the row tag.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Raw row text without the trailing newline.</summary>
    public string Raw { get; set; } = string.Empty;

    /// <summary>Row payload after the id, optional tag, and optional length prefix.</summary>
    public string Data { get; set; } = string.Empty;

    /// <summary>JSON payload when the row data can be parsed as JSON.</summary>
    public string? Json { get; set; }

    /// <summary>Indicates whether <see cref="Json"/> contains valid JSON.</summary>
    public bool IsJson { get; set; }

    /// <summary>Indicates whether the row ended cleanly in the available stream.</summary>
    public bool IsComplete { get; set; }

    /// <summary>Byte offset where this row starts in the reconstructed stream.</summary>
    public int StreamOffset { get; set; }

    /// <summary>Payload instruction index that contributed the start of this row, when known.</summary>
    public int PayloadIndex { get; set; } = -1;
}

/// <summary>
/// Extracts inline Next.js React Flight payloads from HTML.
/// </summary>
public static class HtmlReactFlightParser {
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>Parses inline React Flight payloads from HTML markup.</summary>
    public static HtmlReactFlightDocument Parse(string html) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        HtmlReactFlightDocument result = new();
        List<byte> stream = new();
        int scriptIndex = 0;
        int payloadIndex = 0;

        foreach (IElement script in document.QuerySelectorAll("script")) {
            string content = script.TextContent ?? string.Empty;
            if (!content.Contains("__next_f", StringComparison.Ordinal)) {
                scriptIndex++;
                continue;
            }

            int scriptInstructionIndex = 0;
            foreach (IReadOnlyList<object?> instruction in ExtractPushInstructions(content)) {
                HtmlReactFlightPayload payload = CreatePayload(
                    instruction,
                    payloadIndex,
                    scriptIndex,
                    scriptInstructionIndex,
                    stream.Count);

                AppendStreamBytes(payload, stream);
                result.Payloads.Add(payload);
                payloadIndex++;
                scriptInstructionIndex++;
            }

            scriptIndex++;
        }

        byte[] streamBytes = stream.ToArray();
        result.StreamText = DecodeUtf8(streamBytes);
        foreach (HtmlReactFlightRow row in ParseRows(streamBytes)) {
            row.PayloadIndex = FindPayloadIndex(result.Payloads, row.StreamOffset);
            result.Rows.Add(row);
        }

        return result;
    }

    /// <summary>Downloads HTML from a URL and parses inline React Flight payloads.</summary>
    public static async Task<HtmlReactFlightDocument> ParseUrlAsync(string url, HttpClient? client = null) {
        if (url == null) {
            throw new ArgumentNullException(nameof(url));
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)) {
            throw new ArgumentException("The URL must be an absolute URI.", nameof(url));
        }

        HttpClient http = client ?? HtmlHttpClientFactory.Shared;
        string content = await HtmlUtilities.GetStringWithProperEncodingAsync(http, uri.ToString()).ConfigureAwait(false);
        return Parse(content);
    }

    private static HtmlReactFlightPayload CreatePayload(
        IReadOnlyList<object?> instruction,
        int payloadIndex,
        int scriptIndex,
        int scriptInstructionIndex,
        int streamOffset) {
        int kindCode = TryConvertToInt32(instruction.Count > 0 ? instruction[0] : null, out int code)
            ? code
            : -1;

        HtmlReactFlightPayload payload = new() {
            Index = payloadIndex,
            ScriptIndex = scriptIndex,
            ScriptInstructionIndex = scriptInstructionIndex,
            KindCode = kindCode,
            Kind = Enum.IsDefined(typeof(HtmlReactFlightPayloadKind), kindCode)
                ? (HtmlReactFlightPayloadKind)kindCode
                : HtmlReactFlightPayloadKind.Unknown,
            RawJson = JsonSerializer.Serialize(instruction),
            StreamOffset = streamOffset
        };

        object? value = instruction.Count > 1 ? instruction[1] : null;
        switch (payload.Kind) {
            case HtmlReactFlightPayloadKind.Data:
                payload.Text = value?.ToString() ?? string.Empty;
                break;
            case HtmlReactFlightPayloadKind.Binary:
                payload.Base64 = value?.ToString();
                if (!string.IsNullOrEmpty(payload.Base64)) {
                    try {
                        payload.Bytes = Convert.FromBase64String(payload.Base64);
                    } catch (FormatException) {
                        payload.Bytes = Array.Empty<byte>();
                    }
                }
                break;
            case HtmlReactFlightPayloadKind.FormState:
                payload.FormStateJson = value == null ? null : JsonSerializer.Serialize(value);
                break;
        }

        return payload;
    }

    private static void AppendStreamBytes(HtmlReactFlightPayload payload, List<byte> stream) {
        int start = stream.Count;
        if (payload.Kind == HtmlReactFlightPayloadKind.Data && payload.Text != null) {
            stream.AddRange(Encoding.UTF8.GetBytes(payload.Text));
        } else if (payload.Kind == HtmlReactFlightPayloadKind.Binary && payload.Bytes != null) {
            stream.AddRange(payload.Bytes);
        }

        payload.StreamOffset = start;
        payload.StreamLength = stream.Count - start;
    }

    private static IEnumerable<IReadOnlyList<object?>> ExtractPushInstructions(string script) {
        ParserOptions options = new() {
            Tolerant = true,
            AllowHashBang = true
        };

        AcornimaNode root;
        try {
            root = new Parser(options).ParseScript(script, sourceFile: null, strict: false);
        } catch (ParseErrorException) {
            yield break;
        }

        foreach (AcornimaNode node in Walk(root)) {
            if (!IsNextFlightPushCall(node, out object? argument)) {
                continue;
            }

            object? value = EvaluateLiteral(argument);
            if (value is IReadOnlyList<object?> list && list.Count > 0) {
                yield return list;
            }
        }
    }

    private static IEnumerable<AcornimaNode> Walk(AcornimaNode root) {
        Stack<AcornimaNode> stack = new();
        stack.Push(root);
        while (stack.Count > 0) {
            AcornimaNode node = stack.Pop();
            yield return node;

            List<AcornimaNode> children = node.ChildNodes.ToList();
            for (int index = children.Count - 1; index >= 0; index--) {
                stack.Push(children[index]);
            }
        }
    }

    private static bool IsNextFlightPushCall(AcornimaNode node, out object? argument) {
        argument = null;
        if (node is not CallExpression call) {
            return false;
        }

        if (!IsNextFlightPushMember(call.Callee)) {
            return false;
        }

        foreach (Expression item in call.Arguments) {
            argument = item;
            return argument != null;
        }

        return false;
    }

    private static bool IsNextFlightPushMember(Expression node) {
        if (!IsMemberWithPropertyName(node, "push", out Expression? target)) {
            return false;
        }

        if (IsNextFlightMember(target)) {
            return true;
        }

        if (target is AssignmentExpression assignment && assignment.Left is Expression left) {
            return IsNextFlightMember(left);
        }

        return false;
    }

    private static bool IsNextFlightMember(Expression? node) {
        if (node == null || !IsMemberWithPropertyName(node, "__next_f", out Expression? target)) {
            return false;
        }

        return target is Identifier identifier && identifier.Name == "self";
    }

    private static bool IsMemberWithPropertyName(Expression node, string propertyName, out Expression? target) {
        target = null;
        if (node is not MemberExpression member) {
            return false;
        }

        string? actual = GetPropertyKeyName(member.Property);
        if (!string.Equals(actual, propertyName, StringComparison.Ordinal)) {
            return false;
        }

        target = member.Object;
        return true;
    }

    private static object? EvaluateLiteral(object? node) {
        if (node == null) {
            return null;
        }

        if (node is Literal literal) {
            return literal.Value;
        }

        if (node is ArrayExpression array) {
            List<object?> items = new();
            foreach (Expression? element in array.Elements) {
                items.Add(EvaluateLiteral(element));
            }

            return items;
        }

        if (node is ObjectExpression objectExpression) {
            Dictionary<string, object?> properties = new(StringComparer.Ordinal);
            foreach (Acornima.Ast.Property property in objectExpression.Properties.OfType<Acornima.Ast.Property>()) {
                string? key = GetPropertyKeyName(property.Key);
                if (string.IsNullOrEmpty(key)) {
                    continue;
                }

                properties[key!] = EvaluateLiteral(property.Value);
            }

            return properties;
        }

        if (node is UnaryExpression unary) {
            string? op = unary.Operator.ToString();
            object? value = EvaluateLiteral(unary.Argument);
            if ((op == "-" || op == "UnaryNegation") && value is IConvertible convertible) {
                return -convertible.ToDouble(CultureInfo.InvariantCulture);
            }

            if (op == "!" || op == "LogicalNot") {
                return !HtmlJavaScriptAstUtilities.ToJavaScriptBoolean(value);
            }
        }

        return null;
    }

    private static IEnumerable<HtmlReactFlightRow> ParseRows(byte[] stream) {
        List<HtmlReactFlightRow> rows = new();
        int i = 0;
        while (i < stream.Length) {
            int rowStart = i;
            int id = 0;
            bool idFound = false;
            while (i < stream.Length) {
                byte b = stream[i++];
                if (b == (byte)':') {
                    idFound = true;
                    break;
                }

                int hex = HexValue(b);
                if (hex < 0) {
                    yield break;
                }

                id = (id << 4) | hex;
            }

            if (!idFound) {
                yield break;
            }

            int tag = 0;
            int length = -1;
            int payloadStart;
            int rawEnd;
            bool complete = true;

            if (i < stream.Length && IsLengthPrefixedTag(stream[i])) {
                tag = stream[i++];
                length = 0;
                bool lengthFound = false;
                while (i < stream.Length) {
                    byte b = stream[i++];
                    if (b == (byte)',') {
                        lengthFound = true;
                        break;
                    }

                    int hex = HexValue(b);
                    if (hex < 0) {
                        yield break;
                    }

                    length = (length << 4) | hex;
                }

                if (!lengthFound) {
                    yield break;
                }

                payloadStart = i;
                if (i + length > stream.Length) {
                    length = stream.Length - i;
                    complete = false;
                }

                i += length;
                rawEnd = i;
                if (complete) {
                    i = SkipOptionalRowTerminator(stream, i);
                }
            } else {
                if (i < stream.Length && IsNewlineDelimitedTag(stream[i])) {
                    tag = stream[i++];
                }

                payloadStart = i;
                int newline = IndexOf(stream, (byte)'\n', i);
                if (newline < 0) {
                    newline = stream.Length;
                    complete = false;
                }

                length = newline - payloadStart;
                rawEnd = TrimCarriageReturn(stream, rowStart, newline);
                i = newline < stream.Length ? newline + 1 : newline;
            }

            string data = DecodeUtf8(stream, payloadStart, length);
            string raw = DecodeUtf8(stream, rowStart, Math.Max(0, rawEnd - rowStart));
            HtmlReactFlightRow row = new() {
                Index = rows.Count,
                Id = id,
                Tag = tag == 0 ? null : ((char)tag).ToString(),
                Kind = DescribeTag(tag),
                Raw = raw,
                Data = data,
                IsComplete = complete,
                StreamOffset = rowStart
            };

            if (TryGetJson(data, out string? json)) {
                row.Json = json;
                row.IsJson = true;
            }

            rows.Add(row);
            yield return row;
        }
    }

    private static int SkipOptionalRowTerminator(byte[] stream, int index) {
        if (index >= stream.Length) {
            return index;
        }

        if (stream[index] == (byte)'\r') {
            return index + 1 < stream.Length && stream[index + 1] == (byte)'\n'
                ? index + 2
                : index + 1;
        }

        return stream[index] == (byte)'\n' ? index + 1 : index;
    }

    private static int TrimCarriageReturn(byte[] stream, int rowStart, int rowEnd) {
        return rowEnd > rowStart && stream[rowEnd - 1] == (byte)'\r'
            ? rowEnd - 1
            : rowEnd;
    }

    private static int FindPayloadIndex(IEnumerable<HtmlReactFlightPayload> payloads, int offset) {
        foreach (HtmlReactFlightPayload payload in payloads) {
            if (payload.StreamLength > 0 &&
                offset >= payload.StreamOffset &&
                offset < payload.StreamOffset + payload.StreamLength) {
                return payload.Index;
            }
        }

        return -1;
    }

    private static bool TryGetJson(string data, out string? json) {
        json = null;
        if (string.IsNullOrWhiteSpace(data)) {
            return false;
        }

        char first = data.TrimStart()[0];
        if (first != '{' && first != '[' && first != '"' && first != 't' && first != 'f' &&
            first != 'n' && first != '-' && !char.IsDigit(first)) {
            return false;
        }

        try {
            using JsonDocument _ = JsonDocument.Parse(data);
            json = data;
            return true;
        } catch (JsonException) {
            return false;
        }
    }

    private static string DescribeTag(int tag) {
        return tag switch {
            0 => "Model",
            'I' => "Module",
            'H' => "Hint",
            'E' => "Error",
            'T' => "Text",
            'D' => "Debug",
            'J' => "AsyncInfo",
            'W' => "Console",
            'R' => "ReadableStream",
            'r' => "ReadableByteStream",
            'X' => "AsyncIterable",
            'x' => "AsyncIterator",
            'C' => "StreamClose",
            'A' => "ArrayBuffer",
            'O' => "Int8Array",
            'o' => "Uint8Array",
            'b' => "Buffer",
            'U' => "Uint8ClampedArray",
            'S' => "Int16Array",
            's' => "Uint16Array",
            'L' => "Int32Array",
            'l' => "Uint32Array",
            'G' => "Float32Array",
            'g' => "Float64Array",
            'M' => "BigInt64Array",
            'm' => "BigUint64Array",
            'V' => "DataView",
            _ => $"Tag {((char)tag)}"
        };
    }

    private static bool IsLengthPrefixedTag(byte tag) {
        return tag == 'T' || tag == 'A' || tag == 'O' || tag == 'o' || tag == 'b' ||
               tag == 'U' || tag == 'S' || tag == 's' || tag == 'L' || tag == 'l' ||
               tag == 'G' || tag == 'g' || tag == 'M' || tag == 'm' || tag == 'V';
    }

    private static bool IsNewlineDelimitedTag(byte tag) {
        return (tag > 64 && tag < 91) || tag == '#' || tag == 'r' || tag == 'x';
    }

    private static int IndexOf(byte[] bytes, byte value, int start) {
        for (int index = start; index < bytes.Length; index++) {
            if (bytes[index] == value) {
                return index;
            }
        }

        return -1;
    }

    private static int HexValue(byte value) {
        if (value >= '0' && value <= '9') {
            return value - '0';
        }

        if (value >= 'a' && value <= 'f') {
            return value - 'a' + 10;
        }

        if (value >= 'A' && value <= 'F') {
            return value - 'A' + 10;
        }

        return -1;
    }

    private static bool TryConvertToInt32(object? value, out int result) {
        try {
            if (value is IConvertible convertible) {
                result = convertible.ToInt32(CultureInfo.InvariantCulture);
                return true;
            }
        } catch (FormatException) {
        } catch (OverflowException) {
        }

        result = default;
        return false;
    }

    private static string DecodeUtf8(byte[] bytes) {
        return DecodeUtf8(bytes, 0, bytes.Length);
    }

    private static string DecodeUtf8(byte[] bytes, int index, int count) {
        if (count <= 0) {
            return string.Empty;
        }

        try {
            return StrictUtf8.GetString(bytes, index, count);
        } catch (DecoderFallbackException) {
            return Encoding.UTF8.GetString(bytes, index, count);
        }
    }

    private static string? GetPropertyKeyName(Expression? key) {
        if (key is Identifier identifier) {
            return identifier.Name;
        }

        if (key is Literal literal) {
            return literal.Value?.ToString();
        }

        return null;
    }
}
