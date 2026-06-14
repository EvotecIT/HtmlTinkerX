using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace HtmlTinkerX;

/// <summary>
/// Builds a static extraction plan from page markup using existing HtmlTinkerX parsers.
/// </summary>
public static class HtmlExtractionPlanner {
    private static readonly Regex WordPattern = new(@"\p{L}[\p{L}\p{M}\p{N}'-]*", RegexOptions.Compiled);

    /// <summary>
    /// Analyzes HTML and recommends a PSParseHTML extraction workflow.
    /// </summary>
    /// <param name="html">HTML content to inspect.</param>
    /// <param name="url">Optional source URL used in the suggested command and relative URL analysis.</param>
    /// <returns>An extraction plan with reasons, warnings, and page signals.</returns>
    public static HtmlExtractionPlan Analyze(string html, Uri? url = null) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        HtmlReadableTextResult readable = HtmlParserToText.ExtractReadableText(html);
        IReadOnlyList<HtmlDataItem> dataItems = HtmlParsingToolbox.SelectData(html, baseUri: url);
        List<HtmlFormResult> forms = HtmlParser.ParseFormsWithAngleSharp(html);

        int wordCount = CountWords(readable.Text);
        int scriptCount = document.QuerySelectorAll("script").Length;
        int externalScriptCount = document.QuerySelectorAll("script[src]").Length;
        int linkCount = document.QuerySelectorAll("a[href]").Length;
        int hiddenFieldCount = forms.Sum(static form => form.Fields.Count(static field => field.Type == HtmlFormFieldType.Hidden));
        int appStateCount = dataItems.Count(static item => item.Kind.Equals("AppState", StringComparison.OrdinalIgnoreCase));
        int jsonLdCount = dataItems.Count(static item => item.Kind.Equals("JsonLd", StringComparison.OrdinalIgnoreCase));
        int openGraphCount = dataItems.Count(static item => item.Kind.Equals("OpenGraph", StringComparison.OrdinalIgnoreCase));
        int assetCount = dataItems.Count(static item => item.Kind.Equals("Asset", StringComparison.OrdinalIgnoreCase));
        bool hasAutoSubmitForm = HasAutoSubmitForm(document, forms);
        bool hasLoginForm = HasLoginForm(forms);
        bool hasStructuredData = dataItems.Any(static item =>
            item.Kind.Equals("JsonLd", StringComparison.OrdinalIgnoreCase)
            || item.Kind.Equals("OpenGraph", StringComparison.OrdinalIgnoreCase)
            || item.Kind.Equals("Microdata", StringComparison.OrdinalIgnoreCase)
            || item.Kind.Equals("AppState", StringComparison.OrdinalIgnoreCase)
            || item.Kind.Equals("ScriptData", StringComparison.OrdinalIgnoreCase));
        bool looksLikeJavaScriptShell = LooksLikeJavaScriptShell(document, wordCount, scriptCount, appStateCount, externalScriptCount);

        List<string> reasons = new();
        List<string> warnings = new();
        HtmlExtractionPlanMode mode = ChooseMode(
            hasAutoSubmitForm,
            hasLoginForm,
            looksLikeJavaScriptShell,
            linkCount,
            wordCount,
            forms.Count,
            dataItems.Count,
            reasons,
            warnings);

        HtmlExtractionPlan plan = new() {
            RecommendedMode = mode,
            Confidence = ChooseConfidence(mode, hasAutoSubmitForm, hasLoginForm, looksLikeJavaScriptShell, wordCount, linkCount, dataItems.Count),
            SuggestedCommand = BuildSuggestedCommand(mode, url),
            Reasons = reasons,
            Warnings = warnings,
            Title = readable.Title ?? document.Title ?? string.Empty,
            WordCount = wordCount,
            ScriptCount = scriptCount,
            ExternalScriptCount = externalScriptCount,
            FormCount = forms.Count,
            HiddenFieldCount = hiddenFieldCount,
            LinkCount = linkCount,
            AssetCount = assetCount,
            DataItemCount = dataItems.Count,
            AppStateCount = appStateCount,
            JsonLdCount = jsonLdCount,
            OpenGraphCount = openGraphCount,
            HasAutoSubmitForm = hasAutoSubmitForm,
            HasLoginForm = hasLoginForm,
            HasAppState = appStateCount > 0,
            HasStructuredData = hasStructuredData,
            LooksLikeJavaScriptShell = looksLikeJavaScriptShell
        };

        AddSignalReasons(plan, reasons);
        HtmlExtractionProfile profile = HtmlExtractionProfiles.Recommend(plan, url);
        plan.SuggestedProfileName = profile.Name;
        plan.SuggestedProfileCommand = BuildProfileCommand(profile, url);
        plan.SuggestedProfileReason = BuildProfileReason(profile);
        return plan;
    }

    private static HtmlExtractionPlanMode ChooseMode(
        bool hasAutoSubmitForm,
        bool hasLoginForm,
        bool looksLikeJavaScriptShell,
        int linkCount,
        int wordCount,
        int formCount,
        int dataItemCount,
        List<string> reasons,
        List<string> warnings) {
        if (hasAutoSubmitForm) {
            reasons.Add("Page has a hidden-form auto-submit relay shape.");
            warnings.Add("Relay forms can contain sensitive auth tokens; inspect field names before logging values.");
            return HtmlExtractionPlanMode.BrowserlessRelayCandidate;
        }

        if (hasLoginForm) {
            reasons.Add("Page contains a login form with a password field.");
            warnings.Add("Use browser/session or explicit auth handling; avoid logging credentials and hidden fields.");
            return HtmlExtractionPlanMode.AuthRequired;
        }

        if (looksLikeJavaScriptShell) {
            reasons.Add("Static HTML has low readable text but significant script/app-state signals.");
            return HtmlExtractionPlanMode.RenderedSnapshot;
        }

        if (linkCount >= 20 && wordCount >= 80) {
            reasons.Add("Page has enough links and readable text to be useful as part of a crawl dataset.");
            return HtmlExtractionPlanMode.Crawl;
        }

        if (formCount > 0 || dataItemCount > 0) {
            reasons.Add("Static parsing can extract useful forms or structured data without rendering.");
        } else {
            reasons.Add("Static readable text appears sufficient for first-pass extraction.");
        }

        return HtmlExtractionPlanMode.Static;
    }

    private static HtmlExtractionPlanConfidence ChooseConfidence(
        HtmlExtractionPlanMode mode,
        bool hasAutoSubmitForm,
        bool hasLoginForm,
        bool looksLikeJavaScriptShell,
        int wordCount,
        int linkCount,
        int dataItemCount) {
        if ((mode == HtmlExtractionPlanMode.BrowserlessRelayCandidate && hasAutoSubmitForm)
            || (mode == HtmlExtractionPlanMode.AuthRequired && hasLoginForm)
            || (mode == HtmlExtractionPlanMode.RenderedSnapshot && looksLikeJavaScriptShell)) {
            return HtmlExtractionPlanConfidence.High;
        }

        if (mode == HtmlExtractionPlanMode.Crawl && linkCount >= 20 && wordCount >= 80) {
            return HtmlExtractionPlanConfidence.Medium;
        }

        return dataItemCount > 0 || wordCount >= 40
            ? HtmlExtractionPlanConfidence.Medium
            : HtmlExtractionPlanConfidence.Low;
    }

    private static void AddSignalReasons(HtmlExtractionPlan plan, List<string> reasons) {
        if (plan.WordCount > 0) {
            reasons.Add($"Readable text word count: {plan.WordCount}.");
        }

        if (plan.FormCount > 0) {
            reasons.Add($"Forms detected: {plan.FormCount}; hidden fields: {plan.HiddenFieldCount}.");
        }

        if (plan.ScriptCount > 0) {
            reasons.Add($"Scripts detected: {plan.ScriptCount}; linked scripts: {plan.ExternalScriptCount}.");
        }

        if (plan.DataItemCount > 0) {
            reasons.Add($"Structured data items detected: {plan.DataItemCount}.");
        }
    }

    private static bool HasLoginForm(IEnumerable<HtmlFormResult> forms) =>
        forms.Any(static form => form.Fields.Any(static field => field.Type == HtmlFormFieldType.Password));

    private static bool HasAutoSubmitForm(IDocument document, IReadOnlyList<HtmlFormResult> forms) {
        if (forms.Count != 1) {
            return false;
        }

        HtmlFormResult form = forms[0];
        int namedFieldCount = form.Fields.Count(static field => !string.IsNullOrWhiteSpace(field.Name));
        int hiddenCount = form.Fields.Count(static field => field.Type == HtmlFormFieldType.Hidden);
        bool mostlyHidden = namedFieldCount > 0 && hiddenCount >= Math.Max(1, namedFieldCount - 1);
        bool knownRelayFields = ContainsAnyField(form, "wa", "wresult", "wctx", "SAMLRequest", "SAMLResponse", "RelayState");
        bool autoSubmitScript = document.QuerySelectorAll("script")
            .Select(static script => script.TextContent ?? string.Empty)
            .Any(static script =>
                script.IndexOf(".submit()", StringComparison.OrdinalIgnoreCase) >= 0
                || script.IndexOf("document.forms[0]", StringComparison.OrdinalIgnoreCase) >= 0
                || script.IndexOf("hiddenform", StringComparison.OrdinalIgnoreCase) >= 0);

        return mostlyHidden && (knownRelayFields || autoSubmitScript);
    }

    private static bool ContainsAnyField(HtmlFormResult form, params string[] names) =>
        form.Fields.Any(field => names.Any(name => string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase)));

    private static bool LooksLikeJavaScriptShell(IDocument document, int wordCount, int scriptCount, int appStateCount, int externalScriptCount) {
        string bodyText = Normalize(document.Body?.TextContent ?? string.Empty);
        bool loadingShell = bodyText.Equals("loading", StringComparison.OrdinalIgnoreCase)
            || bodyText.Equals("loading...", StringComparison.OrdinalIgnoreCase)
            || bodyText.Contains("enable javascript", StringComparison.OrdinalIgnoreCase);

        return (wordCount < 40 && (scriptCount >= 2 || externalScriptCount >= 1))
            || (wordCount < 80 && appStateCount > 0)
            || (wordCount < 20 && loadingShell && scriptCount > 0);
    }

    private static string BuildSuggestedCommand(HtmlExtractionPlanMode mode, Uri? url) {
        string target = url == null ? "$html" : "'" + url.AbsoluteUri.Replace("'", "''") + "'";
        return mode switch {
            HtmlExtractionPlanMode.BrowserlessRelayCandidate => url == null
                ? "Invoke-HtmlFormRelay -Content $html -BaseUrl '<current-response-url>'"
                : $"Invoke-HtmlFormRelay -Url {target}",
            HtmlExtractionPlanMode.AuthRequired => url == null
                ? "Invoke-HtmlRendering -Url '<login-protected-url>' -Session"
                : $"Invoke-HtmlRendering -Url {target} -Session",
            HtmlExtractionPlanMode.RenderedSnapshot => url == null
                ? "Invoke-HtmlRendering -Url '<page-url>' -Snapshot -RenderProfile HeavyDynamicPage"
                : $"Invoke-HtmlRendering -Url {target} -Snapshot -RenderProfile HeavyDynamicPage",
            HtmlExtractionPlanMode.Crawl => url == null
                ? "Invoke-HtmlCrawl -Url '<start-url>' -Scenario Dataset -AutoRender"
                : $"Invoke-HtmlCrawl -Url {target} -Scenario Dataset -AutoRender",
            _ => url == null
                ? "Select-HtmlData -Content $html"
                : $"Select-HtmlData -Url {target}"
        };
    }

    private static string BuildProfileCommand(HtmlExtractionProfile profile, Uri? url) {
        string target = url == null ? null! : "'" + url.AbsoluteUri.Replace("'", "''") + "'";
        if (url == null) {
            return profile.SuggestedCommand;
        }

        return profile.Name switch {
            "auth-relay-page" => $"Invoke-HtmlFormRelay -Url {target}",
            "login-protected-page" => $"Invoke-HtmlRendering -Url {target} -Session",
            "app-shell" => $"$snapshot = Invoke-HtmlRendering -Url {target} -Snapshot -RenderProfile HeavyDynamicPage; Invoke-HtmlPageWorkbench -Url {target} -RenderedSnapshot $snapshot",
            "api-docs-content" => $"Invoke-HtmlCrawl -Url {target} -Scenario Dataset -Profile api-docs-content",
            "docs-content" => $"Invoke-HtmlCrawl -Url {target} -Scenario Dataset -Profile docs-content",
            "dataset-page" => $"ConvertTo-HtmlDatasetJsonL -Url {target}",
            _ => $"Invoke-HtmlPageWorkbench -Url {target}"
        };
    }

    private static string BuildProfileReason(HtmlExtractionProfile profile) =>
        profile.ReasonCodes.Count == 0
            ? profile.Description
            : $"{profile.Description} Signals: {string.Join(", ", profile.ReasonCodes)}.";

    private static int CountWords(string text) => WordPattern.Matches(text ?? string.Empty).Count;

    private static string Normalize(string value) =>
        string.Join(" ", (value ?? string.Empty).Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
}
