using System;
using System.Text;
using HtmlTinkerX;

namespace HtmlTinkerX.Tests;

public class HtmlReactFlightParserTests {
    [Fact]
    public void ParseExtractsNextInlinePayloadsAndRows() {
        string html = """
            <!doctype html>
            <html>
            <body>
            <script>
            (self.__next_f=self.__next_f||[]).push([0]);
            self.__next_f.push([1,"1:I[\"/app/page.js\",[\"static/chunk.js\"],\"default\"]\n2:HL[\"/style.css\",\"style\"]\n3:{\"name\":\"Ada\"}\n4:T5,hello\n5:{\"after\":true}\n"]);
            self.__next_f.push([2,{"field":"value"}]);
            </script>
            </body>
            </html>
            """;

        HtmlReactFlightDocument document = HtmlReactFlightParser.Parse(html);

        Assert.Equal(3, document.Payloads.Count);
        Assert.Equal(HtmlReactFlightPayloadKind.Bootstrap, document.Payloads[0].Kind);
        Assert.Equal(HtmlReactFlightPayloadKind.Data, document.Payloads[1].Kind);
        Assert.Equal(HtmlReactFlightPayloadKind.FormState, document.Payloads[2].Kind);
        Assert.Contains("\"field\":\"value\"", document.Payloads[2].FormStateJson);

        Assert.Equal(5, document.Rows.Count);
        Assert.Equal("Module", document.Rows[0].Kind);
        Assert.Equal("Hint", document.Rows[1].Kind);
        Assert.Equal("Model", document.Rows[2].Kind);
        Assert.Equal("Text", document.Rows[3].Kind);
        Assert.Equal("hello", document.Rows[3].Data);
        Assert.True(document.Rows[4].IsJson);
        Assert.Equal("{\"after\":true}", document.Rows[4].Json);
    }

    [Fact]
    public void ParseDecodesBase64BinaryPayloadRows() {
        string row = "a:{\"binary\":true}\n";
        string base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(row));
        string html = $"""
            <html>
            <body>
            <script>
            (self.__next_f=self.__next_f||[]).push([0]);
            self.__next_f.push([3,"{base64}"]);
            </script>
            </body>
            </html>
            """;

        HtmlReactFlightDocument document = HtmlReactFlightParser.Parse(html);

        HtmlReactFlightPayload payload = Assert.Single(document.Payloads, p => p.Kind == HtmlReactFlightPayloadKind.Binary);
        Assert.NotNull(payload.Bytes);
        HtmlReactFlightRow parsedRow = Assert.Single(document.Rows);
        Assert.Equal(10, parsedRow.Id);
        Assert.True(parsedRow.IsJson);
        Assert.Equal("{\"binary\":true}", parsedRow.Json);
        Assert.Equal(payload.Index, parsedRow.PayloadIndex);
    }

    [Fact]
    public void ParsePreservesUnaryValuesInFormStatePayloads() {
        string html = """
            <html>
            <body>
            <script>
            self.__next_f.push([2,{count:-1,enabled:!0,disabled:!1}]);
            </script>
            </body>
            </html>
            """;

        HtmlReactFlightDocument document = HtmlReactFlightParser.Parse(html);

        HtmlReactFlightPayload payload = Assert.Single(document.Payloads);
        Assert.Equal(HtmlReactFlightPayloadKind.FormState, payload.Kind);
        Assert.Equal("{\"count\":-1,\"enabled\":true,\"disabled\":false}", payload.FormStateJson);
        Assert.Equal("[2,{\"count\":-1,\"enabled\":true,\"disabled\":false}]", payload.RawJson);
    }

    [Fact]
    public void ParseIgnoresScriptsThatDoNotContainNextFlightPayloads() {
        HtmlReactFlightDocument document = HtmlReactFlightParser.Parse("<script>const answer = 42;</script>");

        Assert.Empty(document.Payloads);
        Assert.Empty(document.Rows);
    }
}
