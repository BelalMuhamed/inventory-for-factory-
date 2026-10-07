using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Swagger
{
    /// <summary>
    /// Indexes every <c>&lt;member&gt;</c> summary across the solution's generated XML doc files
    /// by its XML member-name id (<c>"T:"</c> for a type, <c>"F:"</c> for a field/enum member,
    /// <c>"P:"</c> for a property, <c>"M:"</c> for a method). Built once, lazily, from whatever
    /// <c>*.xml</c> files sit next to the running assembly — one per project that has
    /// <c>GenerateDocumentationFile</c> enabled (Presentation, ApplicationLayer, DomainLayer as of
    /// Phase S1).
    /// </summary>
    internal sealed class XmlDocIndex
    {
        private static readonly Lazy<XmlDocIndex> LazyInstance = new(Load);

        /// <summary>The shared, process-wide index.</summary>
        public static XmlDocIndex Instance => LazyInstance.Value;

        private readonly IReadOnlyDictionary<string, string> _summariesByMemberId;

        private XmlDocIndex(IReadOnlyDictionary<string, string> summariesByMemberId)
        {
            _summariesByMemberId = summariesByMemberId;
        }

        /// <summary>Looks up the plain-text summary for a member id, or null if none is documented.</summary>
        public string? GetSummary(string memberId) =>
            _summariesByMemberId.TryGetValue(memberId, out string? summary) ? summary : null;

        private static XmlDocIndex Load()
        {
            var summaries = new Dictionary<string, string>();

            foreach (string xmlPath in Directory.EnumerateFiles(AppContext.BaseDirectory, "*.xml"))
            {
                try
                {
                    XDocument doc = XDocument.Load(xmlPath);
                    foreach (XElement member in doc.Descendants("member"))
                    {
                        string? name = member.Attribute("name")?.Value;
                        XElement? summaryElement = member.Element("summary");
                        if (name is null || summaryElement is null)
                        {
                            continue;
                        }

                        summaries[name] = NormalizeSummary(summaryElement.Value);
                    }
                }
                catch (Exception)
                {
                    // A malformed or unrelated XML file next to the assembly must never break
                    // Swagger generation — enum descriptions are a documentation nicety, not a
                    // hard dependency of the app starting.
                }
            }

            return new XmlDocIndex(summaries);
        }

        // Doc-comment text arrives with the original indentation/line breaks; collapse it to a
        // single readable line for use inside a Swagger schema description.
        private static string NormalizeSummary(string raw) =>
            Regex.Replace(raw, @"\s+", " ").Trim();
    }
}
