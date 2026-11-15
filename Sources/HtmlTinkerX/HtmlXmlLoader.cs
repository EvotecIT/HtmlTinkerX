using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace HtmlTinkerX;

/// <summary>
/// Loads untrusted XML without resolving external resources. Callers choose how a document type declaration is treated.
/// </summary>
internal static class HtmlXmlLoader {
    /// <summary>
    /// Parses XML with the given DTD handling. <see cref="DtdProcessing.Prohibit"/> rejects any DOCTYPE with an
    /// <see cref="XmlException"/>; <see cref="DtdProcessing.Ignore"/> skips it, so its entity declarations are not defined.
    /// </summary>
    internal static XDocument Load(string xml, DtdProcessing dtdProcessing, LoadOptions loadOptions = LoadOptions.None) {
        XmlReaderSettings settings = new() {
            DtdProcessing = dtdProcessing,
            XmlResolver = null
        };

        using StringReader stringReader = new(xml);
        using XmlReader xmlReader = XmlReader.Create(stringReader, settings);
        return XDocument.Load(xmlReader, loadOptions);
    }
}
