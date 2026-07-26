using AngleSharp.Dom;
using HtmlAgilityPack;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>Returns inner text from AngleSharp or HtmlAgilityPack elements and documents.</summary>
/// <example>
///   <summary>Read decoded paragraph text</summary>
///   <code>ConvertFrom-Html -Content $html | Select-HtmlNode -XPath '//p' | Select-HtmlInnerText -DeEntitize</code>
/// </example>
[Cmdlet(VerbsCommon.Select, "HtmlInnerText")]
[OutputType(typeof(string))]
public sealed class CmdletSelectHtmlInnerText : AsyncPSCmdlet {
    /// <summary>AngleSharp or HtmlAgilityPack element, document, attribute, or raw string content.</summary>
    [Parameter(Mandatory = true, ValueFromPipeline = true, Position = 0)]
    public object InputObject { get; set; } = null!;

    /// <summary>Decodes HTML entities in the returned text.</summary>
    [Parameter]
    [Alias("DecodeEntities")]
    public SwitchParameter DeEntitize { get; set; }

    /// <summary>Preserves leading and trailing whitespace.</summary>
    [Parameter]
    public SwitchParameter NoTrim { get; set; }

    /// <summary>Value returned when the extracted text is empty.</summary>
    [Parameter]
    public string? DefaultValue { get; set; }

    /// <inheritdoc />
    protected override Task ProcessRecordAsync() {
        object value = HtmlPipelineInput.Unwrap(InputObject);
        string text = value switch {
            IElement element => element.TextContent,
            IDocument angleDocument => angleDocument.DocumentElement?.TextContent ?? string.Empty,
            HtmlNode node => node.InnerText,
            HtmlDocument document => document.DocumentNode.InnerText,
            HtmlAttribute attribute => attribute.Value,
            string content => content.Contains("<") ? HtmlPipelineInput.ToHtmlNode(content).InnerText : content,
            _ => value.ToString() ?? string.Empty
        };

        if (DeEntitize.IsPresent) {
            text = HtmlEntity.DeEntitize(text) ?? text;
        }

        if (!NoTrim.IsPresent) {
            text = text.Trim();
        }

        if (text.Length == 0 && DefaultValue != null) {
            text = DefaultValue;
        }

        WriteObject(text);
        return Task.CompletedTask;
    }
}
