using System;
using System.Management.Automation;
using AngleSharp.Dom;
using HtmlAgilityPack;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that parses HTML content from a string or URL.
/// </summary>
/// <example>
/// <code>ConvertFrom-HTML -Url https://example.com</code>
/// </example>
[Cmdlet(VerbsData.Convert, "FromHtml", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(object))]
public sealed class CmdletConvertFromHtml : PSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetUrl = "Url";

    /// <summary>HTML content to parse.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    public string Content { get; set; } = string.Empty;

    /// <summary>URL of a HTML page.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetUrl)]
    [Alias("Uri")]
    public Uri Url { get; set; } = null!;

    /// <summary>Selects parsing engine.</summary>
    [Parameter]
    [ValidateSet("AngleSharp", "AgilityPack")]
    public string Engine { get; set; } = "AgilityPack";

    /// <summary>Return raw document object.</summary>
    [Parameter]
    public SwitchParameter Raw { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        if (ParameterSetName == ParameterSetUrl) {
            if (Engine.Equals("AngleSharp", StringComparison.OrdinalIgnoreCase)) {
                IDocument doc = HtmlParser.ParseUrlWithAngleSharpAsync(Url.ToString()).GetAwaiter().GetResult();
                WriteObject(Raw.IsPresent ? doc : doc.DocumentElement);
                return;
            }
            HtmlDocument doc2 = HtmlParser.ParseUrlWithHtmlAgilityPackAsync(Url.ToString()).GetAwaiter().GetResult();
            WriteObject(Raw.IsPresent ? doc2 : doc2.DocumentNode);
            return;
        }

        if (Engine.Equals("AngleSharp", StringComparison.OrdinalIgnoreCase)) {
            IDocument doc = HtmlParser.ParseWithAngleSharp(Content);
            WriteObject(Raw.IsPresent ? doc : doc.DocumentElement);
        } else {
            HtmlDocument doc = HtmlParser.ParseWithHtmlAgilityPack(Content);
            WriteObject(Raw.IsPresent ? doc : doc.DocumentNode);
        }
    }
}
