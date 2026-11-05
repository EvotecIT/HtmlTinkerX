using System;
using System.Net;

namespace HtmlTinkerX;

/// <summary>
/// Response facts that explain why a page could not be read. They never hold the request URL, cookies, credentials or
/// the response body; a block page keeps only a short, redacted title and excerpt so it can be retained in reports.
/// Two failures with the same facts are equal.
/// </summary>
public sealed class HtmlHttpFailure : IEquatable<HtmlHttpFailure> {
    /// <summary>Creates failure facts, for example when restoring them from a stored report.</summary>
    /// <param name="kind">The publisher's explicit challenge, block page or unsuccessful status category.</param>
    /// <param name="statusCode">The original response status, including a successful status on an explicit challenge.</param>
    /// <param name="provider">The provider named by a recognised signature, or null.</param>
    /// <param name="retryAt">The publisher's Retry-After instant, or null.</param>
    /// <param name="title">A redacted block-page title of at most 120 characters, or null.</param>
    /// <param name="excerpt">A redacted block-page text excerpt of at most 400 characters, or null.</param>
    public HtmlHttpFailure(HtmlHttpFailureKind kind, HttpStatusCode statusCode, string? provider = null,
        DateTimeOffset? retryAt = null, string? title = null, string? excerpt = null) {
        Kind = kind;
        StatusCode = statusCode;
        Provider = provider;
        RetryAt = retryAt;
        Title = title;
        Excerpt = excerpt;
    }

    /// <summary>The publisher's explicit challenge, block page or unsuccessful status category.</summary>
    public HtmlHttpFailureKind Kind { get; }

    /// <summary>The original response status.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>The provider named by a recognised signature (Cloudflare, Azure Front Door, Akamai), or null.</summary>
    public string? Provider { get; }

    /// <summary>
    /// The publisher's Retry-After instant, or null when absent or invalid. Delta values are relative to classification
    /// time. Callers own their bounded scheduling policy; this value does not authorize automatic retries.
    /// </summary>
    public DateTimeOffset? RetryAt { get; }

    /// <summary>The block page's redacted, whitespace-collapsed title, or null.</summary>
    public string? Title { get; }

    /// <summary>The block page's redacted, whitespace-collapsed visible text, without scripts or styles, or null.</summary>
    public string? Excerpt { get; }

    /// <inheritdoc />
    public bool Equals(HtmlHttpFailure? other) => other is not null && (ReferenceEquals(this, other)
        || Kind == other.Kind && StatusCode == other.StatusCode && RetryAt == other.RetryAt
        && string.Equals(Provider, other.Provider, StringComparison.Ordinal)
        && string.Equals(Title, other.Title, StringComparison.Ordinal)
        && string.Equals(Excerpt, other.Excerpt, StringComparison.Ordinal));

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as HtmlHttpFailure);

    /// <inheritdoc />
    public override int GetHashCode() {
        unchecked {
            int hash = ((int)Kind * 397) ^ (int)StatusCode;
            hash = (hash * 397) ^ RetryAt.GetHashCode();
            hash = (hash * 397) ^ (Provider == null ? 0 : StringComparer.Ordinal.GetHashCode(Provider));
            hash = (hash * 397) ^ (Title == null ? 0 : StringComparer.Ordinal.GetHashCode(Title));
            return (hash * 397) ^ (Excerpt == null ? 0 : StringComparer.Ordinal.GetHashCode(Excerpt));
        }
    }

    /// <inheritdoc />
    public override string ToString() => HtmlResponseClassifier.Describe(this, expectedContent: null);
}

/// <summary>
/// A publisher response that prevented a page from being read. Derives from <see cref="HttpRequestException"/>, so
/// existing handlers keep working, and carries <see cref="Failure"/> for callers that need to say why.
/// </summary>
public sealed class HtmlHttpException : HttpRequestException {
    /// <summary>Recreates a typed failure, for example from facts retained in a report.</summary>
    /// <param name="failure">Safe response facts from the original failure.</param>
    /// <param name="expectedContent">Optional caller-owned format label, never publisher content or credentials.</param>
    public HtmlHttpException(HtmlHttpFailure failure, string? expectedContent = null)
        : this(HtmlResponseClassifier.Describe(failure ?? throw new ArgumentNullException(nameof(failure)), expectedContent), failure) { }

    internal HtmlHttpException(string message, HtmlHttpFailure failure)
#if NET5_0_OR_GREATER
        : base(message, null, failure.StatusCode) {
#else
        : base(message) {
#endif
        Failure = failure;
    }

    /// <summary>The safe response facts, suitable for retention without serializing the exception.</summary>
    public HtmlHttpFailure Failure { get; }

    /// <summary>The failure category.</summary>
    public HtmlHttpFailureKind Kind => Failure.Kind;

    /// <summary>The provider named by a recognised signature, or null.</summary>
    public string? Provider => Failure.Provider;

    /// <summary>The publisher's Retry-After instant, or null.</summary>
    public DateTimeOffset? RetryAt => Failure.RetryAt;
}
