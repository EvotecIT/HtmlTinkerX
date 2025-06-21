using System.Diagnostics;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

[Cmdlet(VerbsCommon.Show, "HTMLHar")]
public sealed class CmdletShowHtmlHar : AsyncPSCmdlet {
    [Parameter(Mandatory = true, Position = 0)]
    [Alias("File")]
    public string Path { get; set; } = string.Empty;

    [Parameter]
    public string? OutFile { get; set; }

    [Parameter]
    public SwitchParameter Open { get; set; }

    protected override async Task ProcessRecordAsync() {
        string harContent = await HtmlUtilities.ReadFileCheckedAsync(Path).ConfigureAwait(false);
        string resolved = HtmlUtilities.ResolvePath(Path);
        string outPath = OutFile is null
            ? System.IO.Path.ChangeExtension(resolved, ".html")
            : HtmlUtilities.ResolvePath(OutFile);
        string html = $$"""
<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8' />
<title>HAR Viewer</title>
<style>
body { font-family: Arial, sans-serif; }
table { border-collapse: collapse; width: 100%; }
th, td { border: 1px solid #ccc; padding: 4px; text-align: left; }
thead { background: #eee; }
</style>
</head>
<body>
<table>
<thead>
<tr><th>Method</th><th>URL</th><th>Status</th></tr>
</thead>
<tbody id='entries'></tbody>
</table>
<script>
const har = {{harContent}};
const entries = har.log.entries || [];
const tbody = document.getElementById('entries');
for (const e of entries) {
    const tr = document.createElement('tr');
    tr.innerHTML = `<td>${e.request.method}</td><td>${e.request.url}</td><td>${e.response.status}</td>`;
    tbody.appendChild(tr);
}
</script>
</body>
</html>
""";
#if NETSTANDARD2_0 || NETFRAMEWORK
        System.IO.File.WriteAllText(outPath, html);
#else
        await System.IO.File.WriteAllTextAsync(outPath, html, CancelToken).ConfigureAwait(false);
#endif
        if (Open.IsPresent) {
            try {
                Process.Start(new ProcessStartInfo {
                    FileName = outPath,
                    UseShellExecute = true,
                });
            } catch (System.Exception ex) {
                WriteVerbose($"Failed to open file '{outPath}': {ex.Message}");
            }
        } else {
            WriteObject(outPath);
        }
    }
}
