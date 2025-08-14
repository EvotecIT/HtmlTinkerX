using HtmlTinkerX;
using Microsoft.Playwright;
using System.Management.Automation;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that creates a new <see cref="HtmlCookie"/> instance.
/// </summary>
[Cmdlet(VerbsCommon.New, "HtmlBrowserCookie")]
[OutputType(typeof(HtmlCookie))]
[Alias("New-HTMLCookie")]
public sealed class CmdletNewHtmlBrowserCookie : PSCmdlet {
    /// <summary>Cookie name.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Cookie value.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    public string Value { get; set; } = string.Empty;

    /// <summary>Cookie domain.</summary>
    [Parameter]
    public string? Domain { get; set; }

    /// <summary>Cookie path.</summary>
    [Parameter]
    public string? Path { get; set; }

    /// <summary>Cookie URL.</summary>
    [Parameter]
    public string? Url { get; set; }

    /// <summary>Cookie expiration time as UNIX timestamp.</summary>
    [Parameter]
    public long? Expires { get; set; }

    /// <summary>Mark cookie as HTTP only.</summary>
    [Parameter]
    public SwitchParameter HttpOnly { get; set; }

    /// <summary>Mark cookie as secure.</summary>
    [Parameter]
    public SwitchParameter Secure { get; set; }

    /// <summary>SameSite attribute.</summary>
    [Parameter]
    public SameSiteAttribute? SameSite { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        HtmlCookie cookie = new() {
            Name = Name,
            Value = Value,
            Domain = Domain,
            Path = Path,
            Url = Url,
            Expires = Expires,
            HttpOnly = HttpOnly.IsPresent ? true : null,
            Secure = Secure.IsPresent ? true : null,
            SameSite = SameSite
        };
        WriteObject(cookie);
    }
}