using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace HtmlTinkerX;

public static partial class HtmlBrowser {
    /// <summary>One validated browser manifest entry shared by layout and runtime discovery.</summary>
    private sealed class BrowserManifestEntry {
        internal string Name { get; }
        internal string Revision { get; }
        internal IReadOnlyDictionary<string, string> RevisionOverrides { get; }

        internal BrowserManifestEntry(string name, string revision, IReadOnlyDictionary<string, string> overrides) {
            Name = name;
            Revision = revision;
            RevisionOverrides = overrides;
        }
    }

    private static bool IsValidBrowserManifest(string manifestPath) => TryReadBrowserManifest(manifestPath, out _);

    /// <summary>
    /// Treats malformed JSON shapes as driver corruption. Unsafe path components fail clearly before
    /// mutation rather than being interpreted as runtime paths or passed to the SDK CLI.
    /// An empty browsers array remains a valid manifest with the existing runtime fallback behavior.
    /// </summary>
    private static bool TryReadBrowserManifest(string manifestPath, out IReadOnlyList<BrowserManifestEntry> entries) {
        entries = Array.Empty<BrowserManifestEntry>();
        try {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("browsers", out JsonElement browsers)
                || browsers.ValueKind != JsonValueKind.Array) return false;

            var parsed = new List<BrowserManifestEntry>();
            foreach (JsonElement browser in browsers.EnumerateArray()) {
                if (browser.ValueKind != JsonValueKind.Object
                    || !browser.TryGetProperty("name", out JsonElement nameElement) || nameElement.ValueKind != JsonValueKind.String
                    || !browser.TryGetProperty("revision", out JsonElement revisionElement) || revisionElement.ValueKind != JsonValueKind.String) return false;
                string? name = nameElement.GetString();
                string? revision = revisionElement.GetString();
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(revision)) return false;

                var overrides = new Dictionary<string, string>(StringComparer.Ordinal);
                if (browser.TryGetProperty("revisionOverrides", out JsonElement overridesElement)) {
                    if (overridesElement.ValueKind != JsonValueKind.Object) return false;
                    foreach (JsonProperty property in overridesElement.EnumerateObject()) {
                        if (property.Value.ValueKind != JsonValueKind.String) return false;
                        string? value = property.Value.GetString();
                        if (string.IsNullOrWhiteSpace(value)) return false;
                        overrides[property.Name] = value!;
                    }
                }
                parsed.Add(new BrowserManifestEntry(name!, revision!, overrides));
            }
            foreach (BrowserManifestEntry browser in parsed) {
                ValidateRuntimeManifestComponent(browser.Name, "browser name");
                ValidateRuntimeManifestComponent(browser.Revision, "revision", digitsOnly: true);
                foreach (KeyValuePair<string, string> revisionOverride in browser.RevisionOverrides) {
                    ValidateRuntimeManifestComponent(revisionOverride.Key, "revision override platform");
                    ValidateRuntimeManifestComponent(revisionOverride.Value, "revision override", digitsOnly: true);
                }
            }
            entries = parsed;
            return true;
        } catch (IOException) {
            return false;
        } catch (UnauthorizedAccessException) {
            return false;
        } catch (JsonException) {
            return false;
        }
    }

    /// <summary>Accepts SDK component grammar, including dotted host platforms and decimal revisions.</summary>
    private static void ValidateRuntimeManifestComponent(string value, string field, bool digitsOnly = false) {
        bool valid = value.Length > 0 && value != "." && value != ".."
            && value.All(character => character >= '0' && character <= '9'
                || !digitsOnly && (character >= 'a' && character <= 'z' || character >= 'A' && character <= 'Z'
                    || character == '-' || character == '_' || character == '.'));
        if (!valid) {
            throw new InvalidOperationException($"The Playwright browser manifest contains an unsafe {field} component. Repair the manifest before installing or cleaning runtimes.");
        }
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> GetExpectedRuntimeDirectories(string browserRoot, HtmlBrowserEngine engine) {
        var expected = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        string manifestPath = Path.Combine(GetDriverPath(), "package", "browsers.json");
        if (!TryReadBrowserManifest(manifestPath, out IReadOnlyList<BrowserManifestEntry> browsers)) return expected;

        string[] requiredNames = engine == HtmlBrowserEngine.Chromium
            ? new[] { "chromium", "chromium-headless-shell" }
            : new[] { engine.ToString().ToLowerInvariant() };
        var required = new HashSet<string>(requiredNames, StringComparer.OrdinalIgnoreCase);
        foreach (BrowserManifestEntry browser in browsers) {
            if (!required.Contains(browser.Name)) continue;
            string selectedName = browser.Name;
            string selectedRevision = browser.Revision;
            string hostPlatform = GetCurrentPlaywrightHostPlatform();
            if (browser.RevisionOverrides.TryGetValue(hostPlatform, out string? revisionOverride)) {
                selectedName = $"{browser.Name}_{hostPlatform}_special";
                selectedRevision = revisionOverride!;
            }
            expected[browser.Name] = new[] { BuildRuntimeDirectory(browserRoot, selectedName, selectedRevision) };
        }
        return expected.Count == required.Count
            ? expected : new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
    }

    private static string BuildRuntimeDirectory(string browserRoot, string name, string revision) {
        ValidateRuntimeManifestComponent(name, "runtime directory name");
        ValidateRuntimeManifestComponent(revision, "runtime revision", digitsOnly: true);
        string root = Path.GetFullPath(browserRoot);
        string directory = Path.GetFullPath(Path.Combine(root, name.Replace('-', '_') + "-" + revision));
        RequireBrowserRuntimeDescendant(directory, root);
        return directory;
    }

    /// <summary>
    /// Checks normalized and physical containment before any selected runtime deletion. Read-only complete
    /// runtime detection can still reuse links; mutation requires direct paths inside the selected cache.
    /// </summary>
    private static string GetContainedBrowserRuntimeIdentity(string browserRoot, string directory) {
        RequireBrowserRuntimeDescendant(Path.GetFullPath(directory), Path.GetFullPath(browserRoot));
        string rootIdentity = HtmlBrowserFileSystemPath.GetValidatedInstallationPathIdentity(browserRoot);
        string identity = HtmlBrowserFileSystemPath.GetValidatedInstallationPathIdentity(directory);
        RequireBrowserRuntimeDescendant(identity, rootIdentity);
        return identity;
    }

    private static void RequireBrowserRuntimeDescendant(string directory, string root) {
        if (string.Equals(directory, root, StringComparison.Ordinal)
            || !HtmlBrowserFileSystemPath.IsWithinPathIdentity(directory, root)) {
            throw new InvalidOperationException("A Playwright browser manifest runtime path escapes the selected browser cache root.");
        }
    }
}
