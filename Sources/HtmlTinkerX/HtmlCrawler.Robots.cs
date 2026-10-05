using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace HtmlTinkerX;

public static partial class HtmlCrawler {
    private static async Task<RobotsDocument?> GetRobotsDocumentAsync(
        Uri uri,
        HttpClient client,
        HtmlCrawlOptions options,
        IDictionary<string, RobotsDocument?> cache,
        CancellationToken cancellationToken) {
        string hostKey = GetHostKey(uri);
        if (cache.TryGetValue(hostKey, out RobotsDocument? cached)) {
            return cached;
        }

        Uri robotsUri = new UriBuilder(uri.Scheme, uri.Host, uri.Port) {
            Path = "/robots.txt"
        }.Uri;

        try {
            using CancellationTokenSource requestTimeout = HtmlUtilities.CreateRequestTimeoutTokenSource(client, cancellationToken);
            CancellationToken requestToken = requestTimeout.Token;
            using HttpResponseMessage response = await client.GetAsync(robotsUri, HttpCompletionOption.ResponseHeadersRead, requestToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) {
                cache[hostKey] = new RobotsDocument();
                return cache[hostKey];
            }

            byte[] bytes = await HtmlUtilities.ReadResponseBytesAsync(response, options.MaximumPageResponseBytes, requestToken, options.PageResponseBudget).ConfigureAwait(false);
            string text = Encoding.UTF8.GetString(bytes);
            RobotsDocument robots = ParseRobots(text, options.RobotsUserAgent);
            cache[hostKey] = robots;
            return robots;
        } catch (HtmlCrawlBudgetExceededException) {
            throw;
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch {
            options.PageResponseBudget?.ThrowIfExceeded();
            cache[hostKey] = new RobotsDocument();
            return cache[hostKey];
        }
    }

    private static async Task<int> GetEffectiveDelayAsync(
        Uri currentUri,
        HttpClient client,
        HtmlCrawlOptions options,
        IDictionary<string, RobotsDocument?> cache,
        CancellationToken cancellationToken) {
        int delay = options.DelayMs;
        if (!options.RespectRobotsTxt) {
            return delay;
        }

        RobotsDocument? robots = await GetRobotsDocumentAsync(currentUri, client, options, cache, cancellationToken).ConfigureAwait(false);
        if (robots?.CrawlDelayMs is int robotsDelay) {
            return Math.Max(delay, robotsDelay);
        }

        return delay;
    }

    private static RobotsDocument ParseRobots(string content, string crawlerUserAgent) {
        IReadOnlyList<HtmlRobotsRule> parsed = HtmlRobotsParser.Parse(content);
        var groups = parsed.GroupBy(rule => rule.GroupIndex).Select(group => new {
            Rules = group.ToArray(),
            Agents = group.Where(rule => rule.Directive.Equals("User-agent", StringComparison.OrdinalIgnoreCase)).Select(rule => rule.Value).ToArray()
        }).ToArray();
        int MatchLength(string[] agents) => agents.Where(agent => agent != "*" && agent.Length > 0
            && crawlerUserAgent.IndexOf(agent, StringComparison.OrdinalIgnoreCase) >= 0).Select(agent => agent.Length).DefaultIfEmpty(-1).Max();
        int bestLength = groups.Select(group => MatchLength(group.Agents)).DefaultIfEmpty(-1).Max();
        RobotsDocument document = new();
        document.SitemapUrls.AddRange(parsed.Where(rule => rule.Directive.Equals("Sitemap", StringComparison.OrdinalIgnoreCase)).Select(rule => rule.Value));
        foreach (var group in groups.Where(group => bestLength >= 0
            ? MatchLength(group.Agents) == bestLength : group.Agents.Contains("*"))) {
            foreach (HtmlRobotsRule rule in group.Rules.GroupBy(rule => rule.LineNumber).Select(line => line.First())) {
                bool allow = rule.Directive.Equals("Allow", StringComparison.OrdinalIgnoreCase);
                if ((allow || rule.Directive.Equals("Disallow", StringComparison.OrdinalIgnoreCase)) && rule.Value.Length > 0) {
                    document.Rules.Add(new RobotsRule { Allow = allow, Path = NormalizeRobotsPath(rule.Value) });
                }
                if (rule.Directive.Equals("Crawl-delay", StringComparison.OrdinalIgnoreCase)
                    && double.TryParse(rule.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double seconds)
                    && seconds >= 0 && seconds <= int.MaxValue / 1000d) {
                    document.CrawlDelayMs = Math.Max(document.CrawlDelayMs ?? 0, (int)Math.Round(seconds * 1000));
                }
            }
        }
        return document;
    }

    private static string NormalizeRobotsPath(string value) {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        StringBuilder normalized = new();
        for (int index = 0; index < bytes.Length; index++) {
            byte current = bytes[index];
            if (current == '%' && index + 2 < bytes.Length
                && byte.TryParse(Encoding.ASCII.GetString(bytes, index + 1, 2), System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out byte escaped)) {
                bool unreserved = (escaped >= 'a' && escaped <= 'z') || (escaped >= 'A' && escaped <= 'Z')
                    || (escaped >= '0' && escaped <= '9') || escaped == '-' || escaped == '.' || escaped == '_' || escaped == '~';
                normalized.Append(unreserved ? ((char)escaped).ToString() : "%" + escaped.ToString("X2"));
                index += 2;
            } else if (current >= 128) {
                normalized.Append('%').Append(current.ToString("X2"));
            } else {
                normalized.Append((char)current);
            }
        }
        return normalized.ToString();
    }

    private static bool IsAllowedByRobots(RobotsDocument robots, Uri uri) {
        string target = NormalizeRobotsPath(string.IsNullOrEmpty(uri.PathAndQuery) ? "/" : uri.PathAndQuery);
        int bestLength = -1;
        bool allowed = true;
        foreach (RobotsRule rule in robots.Rules) {
            bool anchored = rule.Path.EndsWith("$", StringComparison.Ordinal);
            string pattern = anchored ? rule.Path.Substring(0, rule.Path.Length - 1) : rule.Path;
            string expression = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + (anchored ? "$" : string.Empty);
            try {
                if (!Regex.IsMatch(target, expression, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))) continue;
            } catch (RegexMatchTimeoutException) {
                // Permission cannot be established safely for this candidate; keep the crawl running.
                return false;
            }
            int length = pattern.Replace("*", string.Empty).Length;
            if (length > bestLength || (length == bestLength && rule.Allow)) {
                bestLength = length;
                allowed = rule.Allow;
            }
        }
        return allowed;
    }
}
