using System;
using System.IO;
using System.Text.Json;

namespace HtmlTinkerX;

public static partial class HtmlBrowser {
    /// <summary>
    /// Gets the installed Playwright JavaScript driver version. Call <see cref="EnsureInstalledAsync"/>
    /// first. This version can differ from the Microsoft.Playwright assembly version when its
    /// NuGet release bundles a JavaScript patch release.
    /// </summary>
    /// <returns>The version declared by the installed driver's package manifest.</returns>
    /// <exception cref="InvalidDataException">The installed driver has no package version.</exception>
    public static string GetInstalledDriverVersion() {
        try {
            using JsonDocument package = JsonDocument.Parse(File.ReadAllText(
                Path.Combine(GetDriverPath(), "package", "package.json")));
            if (package.RootElement.ValueKind != JsonValueKind.Object
                || !package.RootElement.TryGetProperty("version", out JsonElement version)
                || version.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(version.GetString())) {
                throw new InvalidDataException("The installed Playwright driver package has no version.");
            }
            return version.GetString()!;
        } catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException) {
            throw new InvalidDataException("The installed Playwright driver package version could not be read. Repair the driver installation.", ex);
        }
    }
}
