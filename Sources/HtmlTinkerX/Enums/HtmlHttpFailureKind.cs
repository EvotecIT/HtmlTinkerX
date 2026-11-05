namespace HtmlTinkerX;

/// <summary>The publisher response that prevented a page from being read; separate from network or parsing failures.</summary>
public enum HtmlHttpFailureKind {
    /// <summary>The publisher explicitly identified an interactive verification response, such as Cloudflare's <c>cf-mitigated: challenge</c>.</summary>
    Challenge,
    /// <summary>The publisher returned HTTP 401 and requires authentication.</summary>
    AuthenticationRequired,
    /// <summary>The publisher returned HTTP 403 without a recognised challenge or block-page signature.</summary>
    AccessDenied,
    /// <summary>The publisher returned HTTP 429 and is limiting requests.</summary>
    RateLimited,
    /// <summary>Another unsuccessful HTTP response; the original status is available on the failure.</summary>
    HttpError,
    /// <summary>A CDN or web application firewall returned a block page recognised by a named provider signature.</summary>
    BlockPage
}

/// <summary>What <see cref="HtmlFetchPolicy.OnBlocked"/> asks <see cref="HtmlFetchHandler"/> to do with a blocked response.</summary>
public enum HtmlBlockedAction {
    /// <summary>Throw <see cref="HtmlHttpException"/> with the classified failure.</summary>
    Fail,
    /// <summary>Send the request again after the retry delay, within <see cref="HtmlFetchPolicy.RetryCount"/>; GET and HEAD only.</summary>
    Retry,
    /// <summary>Return HTML produced by <see cref="HtmlFetchPolicy.Renderer"/> as a successful response.</summary>
    Render
}
