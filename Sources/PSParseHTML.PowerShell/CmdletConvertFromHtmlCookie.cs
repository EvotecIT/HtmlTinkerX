using HtmlTinkerX;
using System.IO;
using System.Management.Automation;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Converts various cookie representations into <see cref="HtmlCookie"/> objects.
/// </summary>
/// <example>
///   <code>ConvertFrom-HTMLCookie -Content "Set-Cookie: id=42; Path=/" -Format SetCookie</code>
/// </example>
[Cmdlet(VerbsData.ConvertFrom, "HTMLCookie", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(HtmlCookie))]
public sealed class CmdletConvertFromHtmlCookie : PSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetFile = "File";

    /// <summary>Cookie data to parse.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    public string Content { get; set; } = string.Empty;

    /// <summary>Path to a cookie file.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFile)]
    [Alias("File")]
    public string Path { get; set; } = string.Empty;

    /// <summary>Input format.</summary>
    [Parameter]
    public HtmlCookieFormat Format { get; set; } = HtmlCookieFormat.Netscape;

    /// <inheritdoc />
    protected override void ProcessRecord() {
        string data = ParameterSetName == ParameterSetFile
            ? File.ReadAllText(HtmlUtilities.ResolvePath(Path))
            : Content;

        switch (Format) {
            case HtmlCookieFormat.Netscape:
                WriteObject(HtmlCookieParser.ParseNetscapeFile(data), true);
                break;
            case HtmlCookieFormat.SetCookie:
                WriteObject(HtmlCookieParser.ParseSetCookieHeader(data));
                break;
            case HtmlCookieFormat.OrgJson:
                WriteObject(HtmlCookieParser.ParseOrgJsonCookie(data));
                break;
            case HtmlCookieFormat.CookieStore:
                WriteObject(HtmlCookieParser.ParseCookieStoreJson(data));
                break;
            case HtmlCookieFormat.Puppeteer:
                WriteObject(HtmlCookieParser.ParsePuppeteerJson(data), true);
                break;
        }
    }
}
