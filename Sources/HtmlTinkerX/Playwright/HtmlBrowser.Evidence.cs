using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Helper methods for exporting browser evidence bundles.
/// </summary>
public static partial class HtmlBrowser {
    /// <summary>
    /// Exports a set of evidence artifacts from an already loaded browser session.
    /// </summary>
    /// <param name="session">Browser session containing the page to capture.</param>
    /// <param name="outFolder">Output folder for evidence artifacts.</param>
    /// <param name="options">Evidence capture options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Manifest-like result describing the exported artifacts.</returns>
    public static Task<HtmlBrowserEvidenceResult> ExportEvidenceAsync(
        HtmlBrowserSession session,
        string outFolder,
        HtmlBrowserEvidenceOptions? options = null,
        CancellationToken cancellationToken = default) =>
        ExportEvidenceCoreAsync(session, outFolder, networkLog: null, options, cancellationToken);

    /// <summary>
    /// Exports evidence from a rendered crawl page using its page-scoped network log.
    /// </summary>
    /// <param name="context">Rendered crawl page and its prepared browser session.</param>
    /// <param name="outFolder">Output folder for evidence artifacts.</param>
    /// <param name="options">Evidence capture options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Manifest-like result describing the exported artifacts.</returns>
    public static Task<HtmlBrowserEvidenceResult> ExportRenderedPageEvidenceAsync(
        HtmlCrawlRenderedPageContext context,
        string outFolder,
        HtmlBrowserEvidenceOptions? options = null,
        CancellationToken cancellationToken = default) {
        if (context == null) {
            throw new ArgumentNullException(nameof(context));
        }

        return ExportEvidenceCoreAsync(
            context.Session,
            outFolder,
            context.NetworkLog,
            options,
            cancellationToken);
    }

    private static async Task<HtmlBrowserEvidenceResult> ExportEvidenceCoreAsync(
        HtmlBrowserSession session,
        string outFolder,
        IReadOnlyList<HtmlNetworkEntry>? networkLog,
        HtmlBrowserEvidenceOptions? options,
        CancellationToken cancellationToken) {
        if (session == null) {
            throw new ArgumentNullException(nameof(session));
        }

        if (string.IsNullOrWhiteSpace(outFolder)) {
            throw new ArgumentException("Evidence output folder is required.", nameof(outFolder));
        }

        options ??= new HtmlBrowserEvidenceOptions();
        string fullFolder = outFolder.ToFullPath();
        Directory.CreateDirectory(fullFolder);

        string baseFileName = GetSafeBaseFileName(options.BaseFileName);
        string html = string.Empty;
        HtmlBrowserEvidenceResult result = new() {
            OutFolder = fullFolder,
            Url = RedactEvidenceUrl(session.Page.Url, options.RedactSensitiveValues),
            FinalUrl = RedactEvidenceUrl(session.Page.Url, options.RedactSensitiveValues),
            Title = await session.Page.TitleAsync().ConfigureAwait(false),
            CapturedAtUtc = DateTimeOffset.UtcNow,
            UserDataDirectory = session.UserDataDirectory,
            IsPersistent = session.IsPersistent,
            Redacted = options.RedactSensitiveValues
        };

        if (options.Screenshot) {
            string path = Path.Combine(fullFolder, $"{baseFileName}.png");
            await CaptureScreenshotAsync(session.Page, path, CreateEvidenceScreenshotOptions(options, fullPage: false), cancellationToken).ConfigureAwait(false);
            AddArtifact(result, fullFolder, "Screenshot", path, "image/png");
        }

        if (options.FullPageScreenshot) {
            string path = Path.Combine(fullFolder, $"{baseFileName}.full.png");
            await CaptureScreenshotAsync(session.Page, path, CreateEvidenceScreenshotOptions(options, fullPage: true), cancellationToken).ConfigureAwait(false);
            AddArtifact(result, fullFolder, "FullPageScreenshot", path, "image/png");
        }

        if (options.Pdf) {
            string path = Path.Combine(fullFolder, $"{baseFileName}.pdf");
            await SavePagePdfAsync(
                session.Page,
                path,
                new HtmlBrowserPdfOptions(
                    printBackground: true,
                    maskSensitiveElements: options.MaskSensitiveScreenshotElements,
                    maskSelectors: options.ScreenshotMaskSelectors,
                    maskColor: options.ScreenshotMaskColor),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            AddArtifact(result, fullFolder, "Pdf", path, "application/pdf");
        }

        if (options.Html || options.VisibleText || options.Markdown) {
            html = await GetContentAsync(session.Page, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        if (options.Html) {
            string path = Path.Combine(fullFolder, $"{baseFileName}.html");
            await WriteTextAsync(path, RedactEvidenceText(html, options.RedactSensitiveValues), cancellationToken).ConfigureAwait(false);
            AddArtifact(result, fullFolder, "Html", path, "text/html; charset=utf-8");
        }

        if (options.VisibleText) {
            string path = Path.Combine(fullFolder, $"{baseFileName}.txt");
            string text = await GetContentAsync(session.Page, asText: true, cancellationToken: cancellationToken).ConfigureAwait(false);
            await WriteTextAsync(path, RedactEvidenceText(text, options.RedactSensitiveValues), cancellationToken).ConfigureAwait(false);
            AddArtifact(result, fullFolder, "Text", path, "text/plain; charset=utf-8");
        }

        if (options.Markdown) {
            string path = Path.Combine(fullFolder, $"{baseFileName}.md");
            string markdown = HtmlParserToMarkdown.ConvertToMarkdown(html, session.Page.Url);
            await WriteTextAsync(path, RedactEvidenceText(markdown, options.RedactSensitiveValues), cancellationToken).ConfigureAwait(false);
            AddArtifact(result, fullFolder, "Markdown", path, "text/markdown; charset=utf-8");
        }

        if (options.NetworkSummary) {
            string path = Path.Combine(fullFolder, "network-summary.json");
            IEnumerable<HtmlNetworkEntry> effectiveNetworkLog = networkLog ?? session.NetworkLog;
            string json = JsonSerializer.Serialize(CreateNetworkSummary(effectiveNetworkLog, options.RedactSensitiveValues), CreateJsonOptions());
            await WriteTextAsync(path, json, cancellationToken).ConfigureAwait(false);
            AddArtifact(result, fullFolder, "NetworkSummary", path, "application/json; charset=utf-8");
        }

        if (options.SsoHandoffSummary) {
            string path = Path.Combine(fullFolder, "sso-handoff-summary.json");
            IReadOnlyList<HtmlBrowserSsoHandoff> handoffs = await GetSsoHandoffsAsync(
                session,
                new HtmlBrowserSsoHandoffOptions {
                    IncludeSensitiveValues = false,
                    IncludeAllForms = false
                },
                cancellationToken).ConfigureAwait(false);
            result.SsoHandoffCount = handoffs.Count;
            string json = JsonSerializer.Serialize(new {
                Count = handoffs.Count,
                Redacted = true,
                Handoffs = handoffs
            }, CreateJsonOptions());
            await WriteTextAsync(path, json, cancellationToken).ConfigureAwait(false);
            AddArtifact(result, fullFolder, "SsoHandoffSummary", path, "application/json; charset=utf-8");
        }

        if (options.Manifest) {
            string path = Path.Combine(fullFolder, "evidence-manifest.json");
            result.ManifestPath = path;
            string json = JsonSerializer.Serialize(result, CreateJsonOptions());
            await WriteTextAsync(path, json, cancellationToken).ConfigureAwait(false);
        }

        HtmlBrowserRecipeStep recipeStep = new() {
            Action = HtmlBrowserRecipeAction.Evidence,
            OutFolder = outFolder,
            BaseFileName = options.BaseFileName,
            Screenshot = options.Screenshot,
            FullPageScreenshot = options.FullPageScreenshot,
            Pdf = options.Pdf,
            Html = options.Html,
            VisibleText = options.VisibleText,
            Markdown = options.Markdown,
            NetworkSummary = options.NetworkSummary,
            SsoHandoffSummary = options.SsoHandoffSummary,
            MaskSensitiveScreenshotElements = options.MaskSensitiveScreenshotElements,
            ScreenshotMaskColor = options.ScreenshotMaskColor,
            RedactSensitiveValues = options.RedactSensitiveValues,
            Manifest = options.Manifest
        };
        foreach (string selector in options.ScreenshotMaskSelectors) {
            recipeStep.ScreenshotMaskSelectors.Add(selector);
        }
        RecordRecipeStep(session, recipeStep);

        return result;
    }

    private static IEnumerable<object> CreateNetworkSummary(IEnumerable<HtmlNetworkEntry> entries, bool redactSensitiveValues) =>
        entries.Select(static entry => new {
            Url = entry.Url,
            Method = entry.Method.ToString(),
            ResourceType = entry.ResourceType.ToString(),
            Status = entry.Status.HasValue ? (int)entry.Status.Value : (int?)null,
            entry.Started,
            entry.ResponseReceived,
            entry.Finished,
            DurationMs = entry.Duration?.TotalMilliseconds,
            entry.FailureText,
            entry.ResponseBodyTruncated,
            HasResponseBody = entry.ResponseBody != null,
            HasResponseBodyError = entry.ResponseBodyError != null
        })
        .Select(entry => new {
            Url = RedactEvidenceUrl(entry.Url, redactSensitiveValues),
            entry.Method,
            entry.ResourceType,
            entry.Status,
            entry.Started,
            entry.ResponseReceived,
            entry.Finished,
            entry.DurationMs,
            entry.FailureText,
            entry.ResponseBodyTruncated,
            entry.HasResponseBody,
            entry.HasResponseBodyError
        })
        .ToArray();

    private static string RedactEvidenceText(string value, bool redactSensitiveValues) =>
        redactSensitiveValues ? HtmlSensitiveValueRedactor.RedactSensitiveEvidenceText(value) : value;

    private static string RedactEvidenceUrl(string value, bool redactSensitiveValues) =>
        redactSensitiveValues ? HtmlSensitiveValueRedactor.RedactSensitiveQueryValues(value) : value;

    private static ScreenshotOptions CreateEvidenceScreenshotOptions(HtmlBrowserEvidenceOptions options, bool fullPage) =>
        new() {
            FullPage = fullPage,
            MaskSensitiveElements = options.MaskSensitiveScreenshotElements,
            MaskSelectors = options.ScreenshotMaskSelectors,
            MaskColor = options.ScreenshotMaskColor
        };

    private static void AddArtifact(HtmlBrowserEvidenceResult result, string rootFolder, string kind, string path, string contentType) {
        FileInfo file = new(path);
        result.Artifacts.Add(new HtmlBrowserEvidenceArtifact {
            Kind = kind,
            Path = file.FullName,
            RelativePath = GetRelativePath(rootFolder, file.FullName),
            ContentType = contentType,
            SizeBytes = file.Length,
            Sha256 = ComputeSha256(file.FullName)
        });
    }

    private static string ComputeSha256(string path) {
        using SHA256 sha256 = SHA256.Create();
        using FileStream stream = File.OpenRead(path);
        byte[] hash = sha256.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static string GetSafeBaseFileName(string? value) {
        string fileName = string.IsNullOrWhiteSpace(value) ? "page" : value!.Trim();
        foreach (char invalid in Path.GetInvalidFileNameChars()) {
            fileName = fileName.Replace(invalid, '-');
        }

        return string.IsNullOrWhiteSpace(fileName) ? "page" : fileName;
    }

    private static string GetRelativePath(string rootFolder, string path) {
        Uri rootUri = new(AppendDirectorySeparator(Path.GetFullPath(rootFolder)));
        Uri pathUri = new(Path.GetFullPath(path));
        string relative = Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString());
        return relative.Replace('/', Path.DirectorySeparatorChar);
    }

    private static string AppendDirectorySeparator(string path) {
        if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            || path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)) {
            return path;
        }

        return path + Path.DirectorySeparatorChar;
    }

    private static async Task WriteTextAsync(string path, string content, CancellationToken cancellationToken) {
        string fullPath = HtmlUtilities.EnsureDirectoryExists(path);
#if NETSTANDARD2_0 || NETFRAMEWORK
        File.WriteAllText(fullPath, content);
        await Task.CompletedTask.ConfigureAwait(false);
#else
        await File.WriteAllTextAsync(fullPath, content, cancellationToken).ConfigureAwait(false);
#endif
    }

    private static JsonSerializerOptions CreateJsonOptions() {
        JsonSerializerOptions options = new() {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
