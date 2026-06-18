using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace HtmlTinkerX;

/// <summary>
/// Parses SAML response payloads into safe, operator-friendly summaries.
/// </summary>
public static class HtmlSamlResponseParser {
    /// <summary>
    /// Decodes and summarizes a SAMLResponse form value.
    /// </summary>
    /// <param name="samlResponse">Raw, URL-encoded, base64-encoded, or XML SAML response value.</param>
    /// <param name="includeSensitiveValues">Reveal subject values and unredacted XML.</param>
    /// <param name="includeXml">Include decoded XML in the result.</param>
    /// <returns>Safe SAML response summary.</returns>
    public static HtmlSamlResponseSummary Parse(string samlResponse, bool includeSensitiveValues = false, bool includeXml = false) {
        HtmlSamlResponseSummary summary = new() {
            SuggestedCommand = "ConvertFrom-HtmlSamlResponse -SamlResponse $handoff.FormData['SAMLResponse']"
        };

        if (string.IsNullOrWhiteSpace(samlResponse)) {
            summary.ErrorMessage = "SAMLResponse value is empty.";
            return summary;
        }

        if (string.Equals(samlResponse.Trim(), "<redacted>", StringComparison.OrdinalIgnoreCase)) {
            summary.ErrorMessage = "SAMLResponse value is redacted. Rerun Get-HtmlBrowserSsoHandoff with -IncludeSensitiveValues before analyzing it.";
            summary.SuggestedCommand = "Get-HtmlBrowserSsoHandoff -Session $session -IncludeSensitiveValues | ConvertFrom-HtmlSamlResponse";
            return summary;
        }

        if (!TryDecodeXml(samlResponse, out string xml, out string errorMessage)) {
            summary.ErrorMessage = errorMessage;
            return summary;
        }

        XDocument? document;
        try {
            document = LoadSafeXml(xml);
        } catch (Exception ex) when (ex is XmlException || ex is InvalidOperationException) {
            summary.ErrorMessage = $"Decoded SAMLResponse is not valid XML: {ex.Message}";
            return summary;
        }

        XElement root = document.Root!;
        if (!string.Equals(root.Name.LocalName, "Response", StringComparison.OrdinalIgnoreCase)) {
            summary.ErrorMessage = $"Decoded XML root is '{root.Name.LocalName}', not a SAML Response.";
            return summary;
        }

        PopulateResponse(summary, root, includeSensitiveValues);
        if (includeXml) {
            summary.Xml = includeSensitiveValues ? xml : RedactSamlXml(document);
        }

        summary.IsValid = true;
        summary.SuggestedCommand = "Format-List Issuer,StatusCode,Destination,Audiences,NotBefore,NotOnOrAfter,Warnings";
        AddWarnings(summary, includeSensitiveValues, includeXml);
        return summary;
    }

    private static void PopulateResponse(HtmlSamlResponseSummary summary, XElement response, bool includeSensitiveValues) {
        summary.Id = AttributeValue(response, "ID");
        summary.Version = AttributeValue(response, "Version");
        summary.Destination = HtmlSensitiveValueRedactor.RedactSensitiveQueryValues(AttributeValue(response, "Destination"));
        summary.InResponseTo = AttributeValue(response, "InResponseTo");
        summary.IssueInstant = ParseDate(AttributeValue(response, "IssueInstant"));
        summary.Issuer = ElementValue(response.Elements().FirstOrDefault(IsIssuerElement));
        summary.StatusCode = AttributeValue(response.Descendants().FirstOrDefault(IsStatusCodeElement), "Value");
        summary.ContainsEncryptedAssertion = response.Descendants().Any(element => string.Equals(element.Name.LocalName, "EncryptedAssertion", StringComparison.OrdinalIgnoreCase));
        summary.ContainsSignature = response.Descendants().Any(element => string.Equals(element.Name.LocalName, "Signature", StringComparison.OrdinalIgnoreCase));

        XElement? assertion = response.Descendants().FirstOrDefault(element => string.Equals(element.Name.LocalName, "Assertion", StringComparison.OrdinalIgnoreCase));
        if (assertion == null) {
            return;
        }

        summary.AssertionId = AttributeValue(assertion, "ID");
        summary.AssertionIssueInstant = ParseDate(AttributeValue(assertion, "IssueInstant"));
        summary.AssertionIssuer = ElementValue(assertion.Elements().FirstOrDefault(IsIssuerElement));

        string subject = ElementValue(assertion.Descendants().FirstOrDefault(element => string.Equals(element.Name.LocalName, "NameID", StringComparison.OrdinalIgnoreCase)));
        if (!string.IsNullOrWhiteSpace(subject)) {
            summary.ContainsSensitiveValues = true;
            summary.SubjectNameId = includeSensitiveValues ? subject : "<redacted>";
        }

        foreach (string audience in assertion.Descendants().Where(element => string.Equals(element.Name.LocalName, "Audience", StringComparison.OrdinalIgnoreCase)).Select(ElementValue)) {
            if (!string.IsNullOrWhiteSpace(audience) && !summary.Audiences.Contains(audience, StringComparer.OrdinalIgnoreCase)) {
                summary.Audiences.Add(audience);
            }
        }

        XElement? conditions = assertion.Descendants().FirstOrDefault(element => string.Equals(element.Name.LocalName, "Conditions", StringComparison.OrdinalIgnoreCase));
        summary.NotBefore = ParseDate(AttributeValue(conditions, "NotBefore"));
        summary.NotOnOrAfter = ParseDate(AttributeValue(conditions, "NotOnOrAfter"));

        foreach (XElement attribute in assertion.Descendants().Where(element => string.Equals(element.Name.LocalName, "Attribute", StringComparison.OrdinalIgnoreCase))) {
            string name = FirstNonEmpty(AttributeValue(attribute, "Name"), AttributeValue(attribute, "FriendlyName"));
            if (!string.IsNullOrWhiteSpace(name) && !summary.AttributeNames.Contains(name, StringComparer.OrdinalIgnoreCase)) {
                summary.AttributeNames.Add(name);
            }

            if (attribute.Descendants().Any(element => string.Equals(element.Name.LocalName, "AttributeValue", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(element.Value))) {
                summary.ContainsSensitiveValues = true;
            }
        }
    }

    private static void AddWarnings(HtmlSamlResponseSummary summary, bool includeSensitiveValues, bool includeXml) {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (summary.NotOnOrAfter.HasValue && summary.NotOnOrAfter.Value <= now) {
            summary.Warnings.Add("SAML assertion is expired.");
        }

        if (summary.NotBefore.HasValue && summary.NotBefore.Value > now) {
            summary.Warnings.Add("SAML assertion is not valid yet.");
        }

        if (!summary.ContainsSignature) {
            summary.Warnings.Add("No XML Signature element was found. This may be normal for encrypted or transformed samples, but real assertions should be signed by the issuer.");
        }

        if (summary.ContainsEncryptedAssertion) {
            summary.Warnings.Add("EncryptedAssertion is present. Only response-level metadata can be summarized without decrypting the assertion.");
        }

        if (summary.ContainsSensitiveValues && !includeSensitiveValues) {
            summary.Warnings.Add("Subject and attribute values were redacted. Use -IncludeSensitiveValues only for authorized troubleshooting.");
        }

        if (includeXml && includeSensitiveValues) {
            summary.Warnings.Add("Decoded XML includes sensitive assertion values. Avoid storing it in logs or transcripts.");
        }
    }

    private static bool TryDecodeXml(string input, out string xml, out string errorMessage) {
        foreach (string candidate in GetDecodeCandidates(input)) {
            if (string.IsNullOrWhiteSpace(candidate)) {
                continue;
            }

            string trimmed = candidate.Trim();
            if (trimmed.StartsWith("<", StringComparison.Ordinal)) {
                xml = trimmed;
                errorMessage = string.Empty;
                return true;
            }

            foreach (string base64 in GetBase64Candidates(trimmed)) {
                try {
                    byte[] bytes = Convert.FromBase64String(base64);
                    string decoded = Encoding.UTF8.GetString(bytes).Trim('\uFEFF', ' ', '\r', '\n', '\t');
                    if (decoded.StartsWith("<", StringComparison.Ordinal)) {
                        xml = decoded;
                        errorMessage = string.Empty;
                        return true;
                    }
                } catch (FormatException) {
                } catch (DecoderFallbackException) {
                }
            }
        }

        xml = string.Empty;
        errorMessage = "SAMLResponse could not be decoded as XML or base64-encoded XML.";
        return false;
    }

    private static IEnumerable<string> GetDecodeCandidates(string input) {
        string trimmed = input.Trim();
        List<string> candidates = new() {
            trimmed
        };

        string uriDecoded;
        try {
            uriDecoded = Uri.UnescapeDataString(trimmed);
            if (!string.Equals(uriDecoded, trimmed, StringComparison.Ordinal)) {
                candidates.Add(uriDecoded);
            }
        } catch (UriFormatException) {
        }

        string webDecoded = WebUtility.UrlDecode(trimmed);
        if (!string.IsNullOrWhiteSpace(webDecoded) && !string.Equals(webDecoded, trimmed, StringComparison.Ordinal)) {
            candidates.Add(webDecoded);
        }

        return candidates;
    }

    private static IEnumerable<string> GetBase64Candidates(string value) {
        yield return value;

        string noWhitespace = new(value.Where(static c => !char.IsWhiteSpace(c)).ToArray());
        if (!string.Equals(noWhitespace, value, StringComparison.Ordinal)) {
            yield return noWhitespace;
        }

        string plusRestored = value.Replace(' ', '+');
        if (!string.Equals(plusRestored, value, StringComparison.Ordinal)) {
            yield return plusRestored;
            yield return new string(plusRestored.Where(static c => !char.IsWhiteSpace(c)).ToArray());
        }
    }

    private static XDocument LoadSafeXml(string xml) {
        XmlReaderSettings settings = new() {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };

        using StringReader stringReader = new(xml);
        using XmlReader xmlReader = XmlReader.Create(stringReader, settings);
        return XDocument.Load(xmlReader, LoadOptions.PreserveWhitespace);
    }

    private static string RedactSamlXml(XDocument document) {
        XDocument clone = new(document);
        foreach (XElement element in clone.Descendants().Where(IsSensitiveSamlValueElement)) {
            if (!string.IsNullOrWhiteSpace(element.Value)) {
                element.Value = "<redacted>";
            }
        }

        return clone.ToString(SaveOptions.DisableFormatting);
    }

    private static bool IsSensitiveSamlValueElement(XElement element) =>
        string.Equals(element.Name.LocalName, "NameID", StringComparison.OrdinalIgnoreCase)
        || string.Equals(element.Name.LocalName, "AttributeValue", StringComparison.OrdinalIgnoreCase)
        || string.Equals(element.Name.LocalName, "SignatureValue", StringComparison.OrdinalIgnoreCase)
        || string.Equals(element.Name.LocalName, "DigestValue", StringComparison.OrdinalIgnoreCase);

    private static bool IsIssuerElement(XElement element) =>
        string.Equals(element.Name.LocalName, "Issuer", StringComparison.OrdinalIgnoreCase);

    private static bool IsStatusCodeElement(XElement element) =>
        string.Equals(element.Name.LocalName, "StatusCode", StringComparison.OrdinalIgnoreCase);

    private static string ElementValue(XElement? element) =>
        element?.Value.Trim() ?? string.Empty;

    private static string AttributeValue(XElement? element, string name) =>
        element?.Attributes().FirstOrDefault(attribute => string.Equals(attribute.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;

    private static DateTimeOffset? ParseDate(string value) =>
        DateTimeOffset.TryParse(value, out DateTimeOffset parsed) ? parsed : null;

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}
