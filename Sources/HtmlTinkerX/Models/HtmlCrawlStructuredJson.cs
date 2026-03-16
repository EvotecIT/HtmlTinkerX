using System.Collections.Generic;

namespace HtmlTinkerX;

/// <summary>
/// Structured JSON-friendly page data extracted from the crawl.
/// </summary>
public sealed class HtmlCrawlStructuredJson {
    /// <summary>Preset that was resolved for this page when structured extracted fields were built.</summary>
    public HtmlCrawlStructuredJsonPreset ResolvedPreset { get; set; }

    /// <summary>Self-contained document-style representation of the extracted page content.</summary>
    public HtmlCrawlStructuredDocument Document { get; set; } = new();

    /// <summary>Lightweight content-analysis metadata for downstream filtering and search.</summary>
    public HtmlCrawlStructuredContent Content { get; set; } = new();

    /// <summary>Promoted page-level metadata for easier downstream ingestion.</summary>
    public HtmlCrawlStructuredMetadata Metadata { get; set; } = new();

    /// <summary>Detected page-level layout and chrome regions kept separate from main content.</summary>
    public HtmlCrawlStructuredLayout Layout { get; set; } = new();

    /// <summary>Caller-defined extracted fields built from a structured schema, when one was supplied.</summary>
    public IDictionary<string, object?> Extracted { get; set; } = new Dictionary<string, object?>(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>Meta tags extracted from the full HTML document.</summary>
    public IList<HtmlMetaTag> MetaTags { get; set; } = new List<HtmlMetaTag>();

    /// <summary>Open Graph properties extracted from the full HTML document.</summary>
    public HtmlOpenGraph OpenGraph { get; set; } = new();

    /// <summary>Microdata items extracted from the full HTML document.</summary>
    public IList<HtmlMicrodataItem> MicrodataItems { get; set; } = new List<HtmlMicrodataItem>();

    /// <summary>Forms extracted from the full HTML document.</summary>
    public IList<HtmlFormResult> Forms { get; set; } = new List<HtmlFormResult>();

    /// <summary>Lists extracted from the selected page content.</summary>
    public IList<HtmlListResult> Lists { get; set; } = new List<HtmlListResult>();

    /// <summary>Tables extracted from the selected page content.</summary>
    public IList<HtmlTableResult> Tables { get; set; } = new List<HtmlTableResult>();

    /// <summary>Code blocks extracted from the selected page content.</summary>
    public IList<HtmlCrawlStructuredCodeBlock> CodeBlocks { get; set; } = new List<HtmlCrawlStructuredCodeBlock>();

    /// <summary>Semantic code samples extracted from the selected content.</summary>
    public IList<HtmlCrawlStructuredCodeSample> CodeSamples { get; set; } = new List<HtmlCrawlStructuredCodeSample>();

    /// <summary>Breadcrumb trails extracted from page chrome.</summary>
    public IList<HtmlCrawlStructuredBreadcrumbTrail> Breadcrumbs { get; set; } = new List<HtmlCrawlStructuredBreadcrumbTrail>();

    /// <summary>FAQ-style question and answer pairs extracted from the selected content.</summary>
    public IList<HtmlCrawlStructuredFaqItem> FaqItems { get; set; } = new List<HtmlCrawlStructuredFaqItem>();

    /// <summary>Specification-style tables extracted from selected content.</summary>
    public IList<HtmlCrawlStructuredSpecTable> SpecTables { get; set; } = new List<HtmlCrawlStructuredSpecTable>();

    /// <summary>Callout or alert blocks extracted from selected content.</summary>
    public IList<HtmlCrawlStructuredCallout> Callouts { get; set; } = new List<HtmlCrawlStructuredCallout>();

    /// <summary>Prominent action links or buttons extracted from selected content.</summary>
    public IList<HtmlCrawlStructuredPrimaryAction> PrimaryActions { get; set; } = new List<HtmlCrawlStructuredPrimaryAction>();

    /// <summary>API endpoints inferred from docs-style headings and request examples.</summary>
    public IList<HtmlCrawlStructuredApiEndpoint> ApiEndpoints { get; set; } = new List<HtmlCrawlStructuredApiEndpoint>();

    /// <summary>Lightweight API catalog derived from inferred endpoints on the page.</summary>
    public HtmlCrawlStructuredApiCatalog ApiCatalog { get; set; } = new();

    /// <summary>Compact OpenAPI-like snapshot derived from inferred page endpoints.</summary>
    public HtmlCrawlStructuredOpenApiLike OpenApiLike { get; set; } = new();
}
