namespace HtmlTinkerX;

using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Seeds origin-scoped web storage once per capture before source scripts run.</summary>
internal static class HtmlBrowserStorageInitializer {
    private const string ScriptResource = "HtmlTinkerX.Playwright.Scripts.HtmlBrowserStorageInitialization.js";
    private static readonly Lazy<string> Script = new(LoadScript, LazyThreadSafetyMode.ExecutionAndPublication);

    internal static async Task<HtmlBrowserStorageInitialization?> AddAsync(IPage page, HtmlBrowserPdfRequest request) {
        if (request.LocalStorage.Count == 0 && request.SessionStorage.Count == 0) return null;

        string statusKeyValue = "__htmltinkerx_storage_" + Guid.NewGuid().ToString("N");
        string configuration = JsonSerializer.Serialize(new {
            expectedOrigin = request.Source.SecurityOrigin!.GetLeftPart(UriPartial.Authority),
            marker = "__htmltinkerx_seed_" + Guid.NewGuid().ToString("N"),
            statusKey = statusKeyValue,
            local = request.LocalStorage,
            session = request.SessionStorage
        });
        string script = $"({Script.Value})({configuration})";
        await page.AddInitScriptAsync(script).ConfigureAwait(false);
        return new HtmlBrowserStorageInitialization(statusKeyValue);
    }

    private static string LoadScript() {
        Assembly assembly = typeof(HtmlBrowserStorageInitializer).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(ScriptResource)
            ?? throw new InvalidOperationException($"Embedded browser script '{ScriptResource}' was not found.");
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}

internal sealed class HtmlBrowserStorageInitialization {
    private readonly string _statusKey;

    internal HtmlBrowserStorageInitialization(string statusKey) {
        _statusKey = statusKey;
    }

    internal async Task ValidateAsync(IPage page) {
        string statusKey = JsonSerializer.Serialize(_statusKey);
        string? json = await page.EvaluateAsync<string?>($"() => globalThis[{statusKey}] || null").ConfigureAwait(false);
        HtmlBrowserStorageSeedStatus? status = json == null
            ? null
            : JsonSerializer.Deserialize<HtmlBrowserStorageSeedStatus>(json);
        if (status == null || !status.Completed) {
            throw new InvalidOperationException("Requested web storage was not initialized for the capture origin.");
        }
        if (status.Errors.Count > 0) {
            throw new InvalidOperationException("Requested web storage could not be initialized: " + string.Join("; ", status.Errors));
        }
    }

    private sealed class HtmlBrowserStorageSeedStatus {
        [JsonPropertyName("completed")]
        public bool Completed { get; set; }

        [JsonPropertyName("errors")]
        public List<string> Errors { get; set; } = new();
    }
}
