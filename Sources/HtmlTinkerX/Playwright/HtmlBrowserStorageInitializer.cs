namespace HtmlTinkerX;

using Microsoft.Playwright;
using System;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>Seeds origin-scoped web storage once per capture before source scripts run.</summary>
internal static class HtmlBrowserStorageInitializer {
    internal static Task AddAsync(IBrowserContext context, HtmlBrowserPdfRequest request) {
        if (request.LocalStorage.Count == 0 && request.SessionStorage.Count == 0) return Task.CompletedTask;

        string expectedOrigin = JsonSerializer.Serialize(request.Source.SecurityOrigin!.GetLeftPart(UriPartial.Authority));
        string local = JsonSerializer.Serialize(request.LocalStorage);
        string session = JsonSerializer.Serialize(request.SessionStorage);
        string marker = JsonSerializer.Serialize("__htmltinkerx_seed_" + Guid.NewGuid().ToString("N"));
        string script = $"(() => {{ const expectedOrigin = {expectedOrigin}; if (window !== window.top || location.origin !== expectedOrigin) return; const marker = {marker}; try {{ if (sessionStorage.getItem(marker) === '1') return; sessionStorage.setItem(marker, '1'); }} catch {{ }} const local = {local}; const session = {session}; try {{ for (const key of Object.keys(local)) localStorage.setItem(key, local[key]); }} catch {{ }} try {{ for (const key of Object.keys(session)) sessionStorage.setItem(key, session[key]); }} catch {{ }} }})();";
        return context.AddInitScriptAsync(script);
    }
}
