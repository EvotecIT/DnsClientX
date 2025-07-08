using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace DnsClientX;

/// <summary>
/// Provides helper methods for obtaining root DNSSEC trust anchors.
/// </summary>
internal static class RootAnchorHelper {
    private const string Url = "https://data.iana.org/root-anchors/root-anchors.xml";

    /// <summary>
    /// Downloads and parses the current root trust anchor records.
    /// </summary>
    /// <returns>Array of <see cref="RootDsRecord"/> entries.</returns>
    public static async Task<RootDsRecord[]> FetchLatestAsync() {
        using HttpClient client = new();
        string xml = await client.GetStringAsync(Url).ConfigureAwait(false);
        return ParseFromXml(xml);
    }

    /// <summary>
    /// Parses root anchor DS records from an XML string.
    /// </summary>
    /// <param name="xml">Root anchor XML document.</param>
    /// <returns>Array of parsed records.</returns>
    internal static RootDsRecord[] ParseFromXml(string xml) {
        XDocument document = XDocument.Parse(xml);
        DateTime now = DateTime.UtcNow;
        return document
            .Descendants("KeyDigest")
            .Where(d => IsValid(d, now))
            .Select(d => new RootDsRecord(
                ushort.Parse(d.Element("KeyTag")!.Value, CultureInfo.InvariantCulture),
                (DnsKeyAlgorithm)byte.Parse(d.Element("Algorithm")!.Value, CultureInfo.InvariantCulture),
                byte.Parse(d.Element("DigestType")!.Value, CultureInfo.InvariantCulture),
                d.Element("Digest")!.Value.ToUpperInvariant()))
            .ToArray();
    }

    private static bool IsValid(XElement element, DateTime now) {
        DateTime validFrom = DateTime.Parse(element.Attribute("validFrom")!.Value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
        DateTime validUntil = element.Attribute("validUntil") != null
            ? DateTime.Parse(element.Attribute("validUntil")!.Value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal)
            : DateTime.MaxValue;
        return validFrom <= now && now <= validUntil;
    }
}
