using HtmlTinkerX;
using System.IO;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Loads a browserless extraction recipe from disk.
/// </summary>
/// <example>
///   <summary>Load a browserless extraction recipe</summary>
///   <code>Import-HtmlExtractionRecipe -Path .\recipe.json</code>
/// </example>
[Cmdlet(VerbsData.Import, "HtmlExtractionRecipe")]
[OutputType(typeof(HtmlBrowserlessExtractionRecipe))]
public sealed class CmdletImportHtmlExtractionRecipe : AsyncPSCmdlet {
    /// <summary>Recipe JSON path.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    [Alias("File")]
    public string Path { get; set; } = string.Empty;

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        string fullPath = Path.ToFullPath();
        string json = await Task.Run(() => File.ReadAllText(fullPath), CancelToken).ConfigureAwait(false);
        WriteObject(HtmlBrowserlessExtraction.DeserializeRecipe(json));
    }
}
