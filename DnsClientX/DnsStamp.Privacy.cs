using System;
using System.Net;
using System.Text;

namespace DnsClientX;

public static partial class DnsStamp {
    /// <summary>Identifies privacy stamp roles so adapters can preserve their distinct identity model.</summary>
    /// <param name="stamp">A DNS stamp to inspect without loading a provider.</param>
    /// <returns>True for DNSCrypt resolver/relay and ODoH target/relay protocols.</returns>
    public static bool IsPrivacyStamp(string? stamp) {
        if (!IsStamp(stamp)) { return false; }
        if (stamp!.Length > 8192) { throw new FormatException("DNS stamp exceeds the 8192 character limit."); }
        byte[] bytes = DecodeBase64Url(stamp.Substring(Scheme.Length));
        return bytes.Length != 0 && (bytes[0] == 1 || bytes[0] == 5 || bytes[0] == 0x81 || bytes[0] == 0x85);
    }
    /// <summary>Parses a DNSCrypt resolver/relay or ODoH target/relay stamp without enabling a transport.</summary>
    /// <param name="stamp">An sdns:// stamp acquired from a trusted source.</param>
    /// <returns>The distinct role and trusted identity encoded in the stamp.</returns>
    /// <remarks>Nonempty ODoH relay certificate pins and explicit IP/bootstrap routing are rejected because
    /// the optional HTTPS provider does not implement those policies; they are never silently discarded.</remarks>
    public static DnsPrivacyStamp ParsePrivacy(string stamp) {
        if (stamp == null) { throw new ArgumentNullException(nameof(stamp)); }
        if (!IsStamp(stamp) || stamp.Length > 8192) { throw new FormatException("A privacy DNS stamp must use sdns:// and contain at most 8192 characters."); }
        var reader = new StampReader(DecodeBase64Url(stamp.Substring(Scheme.Length)));
        byte protocol = reader.ReadByte();
        if (protocol != 0x01 && protocol != 0x05 && protocol != 0x81 && protocol != 0x85) {
            throw new NotSupportedException("The stamp does not describe a privacy resolver or relay.");
        }
        ulong properties = protocol == 0x81 ? 0 : reader.ReadProperties();
        DnsPrivacyStamp result;
        if (protocol == 0x01 || protocol == 0x81) {
            string value = ReadPrivacyString(reader, "address");
            var (host, port, _) = SplitHostPort(value, 443);
            if (!IPAddress.TryParse(host, out IPAddress? address) || port < 1 || port > 65535) {
                throw new FormatException("DNSCrypt stamps require an IP address and nonzero port.");
            }
            byte[] key = protocol == 0x01 ? reader.ReadLengthPrefixed("provider public key") : Array.Empty<byte>();
            string? name = protocol == 0x01 ? ReadPrivacyString(reader, "provider name") : null;
            if (protocol == 0x01) {
                if (key.Length != 32) { throw new FormatException("The DNSCrypt provider public key must contain 32 bytes."); }
                if (string.IsNullOrWhiteSpace(name) || name == ".") { throw new FormatException("The DNSCrypt provider name is required."); }
                DnsWireNameCodec.EncodeLabels(name!);
            }
            result = new DnsPrivacyStamp((DnsPrivacyStampKind)protocol, properties, new IPEndPoint(address, port), name, key, null);
        } else {
            if (protocol == 0x85) {
                if (ReadPrivacyString(reader, "relay address").Length != 0) {
                    throw new NotSupportedException("Explicit IP routing in ODoH relay stamps is not supported.");
                }
                reader.ReadHashes();
            }
            string host = ReadPrivacyString(reader, "hostname");
            string path = ReadPrivacyString(reader, "path");
            var (name, port, _) = SplitHostPort(host, 443);
            Uri endpoint = BuildDohUri(name, port, path);
            if (endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0 ||
                string.IsNullOrWhiteSpace(host) || !path.StartsWith("/", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal)) {
                throw new FormatException("ODoH stamp endpoints require an HTTPS host and absolute path without credentials, query or fragment.");
            }
            if (protocol == 0x85 && reader.HasRemaining) { reader.ReadBootstrapAddresses(); }
            result = new DnsPrivacyStamp((DnsPrivacyStampKind)protocol, properties, null, null, null, endpoint);
        }
        if (!reader.EndOfPayload) { throw new FormatException("DNS stamp contains trailing data."); }
        return result;
    }

    private static string ReadPrivacyString(StampReader reader, string field) {
        try { return new UTF8Encoding(false, true).GetString(reader.ReadLengthPrefixed(field)); }
        catch (DecoderFallbackException exception) { throw new FormatException("DNS stamp " + field + " is not valid UTF-8.", exception); }
    }
}
