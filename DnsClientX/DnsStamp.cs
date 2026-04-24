using System;
using System.Collections.Generic;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DnsClientX {
    /// <summary>
    /// Parses and creates DNS stamps for endpoint formats supported by DnsClientX.
    /// </summary>
    public static class DnsStamp {
        private const string Scheme = "sdns://";

        /// <summary>
        /// Parses a DNS stamp into a resolver endpoint.
        /// </summary>
        /// <param name="stamp">The DNS stamp string.</param>
        /// <returns>The parsed resolver endpoint.</returns>
        public static DnsResolverEndpoint Parse(string stamp) {
            if (!TryParse(stamp, out DnsResolverEndpoint? endpoint, out string? error)) {
                throw new FormatException(error ?? "Invalid DNS stamp.");
            }

            return endpoint!;
        }

        /// <summary>
        /// Parses a DNS stamp into a user-facing description.
        /// </summary>
        /// <param name="stamp">The DNS stamp string.</param>
        /// <returns>Parsed stamp information.</returns>
        public static DnsStampInfo Describe(string stamp) {
            DnsResolverEndpoint endpoint = Parse(stamp);
            return new DnsStampInfo {
                Stamp = stamp,
                NormalizedStamp = Create(endpoint),
                Endpoint = endpoint,
                Transport = endpoint.Transport,
                RequestFormat = endpoint.RequestFormat,
                Host = endpoint.Host,
                Port = endpoint.Port,
                DohUrl = endpoint.DohUrl,
                DnsSecOk = endpoint.DnsSecOk == true
            };
        }

        /// <summary>
        /// Attempts to parse a DNS stamp into a resolver endpoint.
        /// </summary>
        /// <param name="stamp">The DNS stamp string.</param>
        /// <param name="endpoint">The parsed endpoint when successful.</param>
        /// <param name="error">A descriptive error when parsing fails.</param>
        /// <returns><c>true</c> when the stamp is supported and valid.</returns>
        public static bool TryParse(string? stamp, out DnsResolverEndpoint? endpoint, out string? error) {
            endpoint = null;
            error = null;

            if (string.IsNullOrWhiteSpace(stamp)) {
                error = "DNS stamp is empty.";
                return false;
            }

            string value = stamp!;
            if (!value.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase)) {
                error = "DNS stamp must start with sdns://.";
                return false;
            }

            byte[] payload;
            try {
                payload = DecodeBase64Url(value.Substring(Scheme.Length));
            } catch (FormatException ex) {
                error = $"DNS stamp payload is not valid base64url: {ex.Message}";
                return false;
            }

            if (payload.Length == 0) {
                error = "DNS stamp payload is empty.";
                return false;
            }

            var reader = new StampReader(payload);
            byte protocol = reader.ReadByte();
            try {
                endpoint = protocol switch {
                    0x00 => ParsePlainDns(reader),
                    0x02 => ParseDoh(reader),
                    0x03 => ParseTlsLike(reader, Transport.Dot, DnsRequestFormat.DnsOverTLS, 853),
                    0x04 => ParseTlsLike(reader, Transport.Quic, DnsRequestFormat.DnsOverQuic, 853),
                    0x01 => throw new NotSupportedException("DNSCrypt stamps are not supported by the core resolver endpoint model."),
                    0x05 => throw new NotSupportedException("Oblivious DoH target stamps are not supported by the core resolver endpoint model."),
                    0x81 => throw new NotSupportedException("Anonymized DNSCrypt relay stamps are not supported by the core resolver endpoint model."),
                    0x85 => throw new NotSupportedException("Oblivious DoH relay stamps are not supported by the core resolver endpoint model."),
                    _ => throw new FormatException($"Unsupported DNS stamp protocol identifier 0x{protocol:X2}.")
                };

                if (!reader.EndOfPayload) {
                    throw new FormatException("DNS stamp contains trailing data.");
                }

                return true;
            } catch (Exception ex) when (ex is FormatException || ex is NotSupportedException || ex is ArgumentException) {
                endpoint = null;
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Creates a DNS stamp for a supported resolver endpoint.
        /// </summary>
        /// <param name="endpoint">The endpoint to encode.</param>
        /// <returns>A DNS stamp string.</returns>
        public static string Create(DnsResolverEndpoint endpoint) {
            if (endpoint == null) {
                throw new ArgumentNullException(nameof(endpoint));
            }

            using var stream = new MemoryStream();
            switch (endpoint.Transport) {
                case Transport.Udp:
                    stream.WriteByte(0x00);
                    WriteProperties(stream, endpoint);
                    WriteLengthPrefixed(stream, BuildAddress(endpoint, 53));
                    break;
                case Transport.Doh:
                    stream.WriteByte(0x02);
                    WriteProperties(stream, endpoint);
                    WriteLengthPrefixed(stream, string.Empty);
                    WriteEmptyVariableLengthSet(stream);
                    WriteDohHostAndPath(stream, endpoint);
                    break;
                case Transport.Dot:
                    stream.WriteByte(0x03);
                    WriteProperties(stream, endpoint);
                    WriteLengthPrefixed(stream, string.Empty);
                    WriteEmptyVariableLengthSet(stream);
                    WriteLengthPrefixed(stream, BuildHostWithPort(endpoint, 853));
                    break;
                case Transport.Quic:
                    stream.WriteByte(0x04);
                    WriteProperties(stream, endpoint);
                    WriteLengthPrefixed(stream, string.Empty);
                    WriteEmptyVariableLengthSet(stream);
                    WriteLengthPrefixed(stream, BuildHostWithPort(endpoint, 853));
                    break;
                default:
                    throw new NotSupportedException($"DNS stamps cannot be generated for transport {endpoint.Transport}.");
            }

            return Scheme + EncodeBase64Url(stream.ToArray());
        }

        /// <summary>
        /// Returns whether a string looks like a DNS stamp.
        /// </summary>
        /// <param name="value">Input value.</param>
        /// <returns><c>true</c> when the value starts with the DNS stamp scheme.</returns>
        public static bool IsStamp(string? value) => value?.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase) == true;

        private static DnsResolverEndpoint ParsePlainDns(StampReader reader) {
            ulong properties = reader.ReadProperties();
            string address = reader.ReadLengthPrefixedString("address");
            reader.EnsureAddress(address, allowEmpty: false);
            return EndpointFromAddress(address, Transport.Udp, DnsRequestFormat.DnsOverUDP, 53, properties);
        }

        private static DnsResolverEndpoint ParseDoh(StampReader reader) {
            ulong properties = reader.ReadProperties();
            string address = reader.ReadLengthPrefixedString("address");
            if (!string.IsNullOrEmpty(address)) {
                reader.EnsureAddress(address, allowEmpty: false);
            }

            reader.ReadHashes();
            string host = reader.ReadLengthPrefixedString("hostname");
            string path = reader.ReadLengthPrefixedString("path");
            if (reader.HasRemaining) {
                reader.ReadBootstrapAddresses();
            }

            if (string.IsNullOrWhiteSpace(host)) {
                throw new FormatException("DoH stamp hostname is required.");
            }

            if (string.IsNullOrEmpty(path) || !path.StartsWith("/", StringComparison.Ordinal)) {
                throw new FormatException("DoH stamp path must start with '/'.");
            }

            (string hostName, int port, AddressFamily? family) = SplitHostPort(host, 443);
            if (!HasExplicitPort(host) && TryGetExplicitPort(address, out int addressPort)) {
                port = addressPort;
            }

            Uri dohUrl = BuildDohUri(hostName, port, path);
            return new DnsResolverEndpoint {
                Transport = Transport.Doh,
                RequestFormat = DnsRequestFormat.DnsOverHttps,
                Host = hostName,
                Port = port,
                Family = family,
                DohUrl = dohUrl,
                DnsSecOk = HasDnsSec(properties) ? true : null
            };
        }

        private static DnsResolverEndpoint ParseTlsLike(StampReader reader, Transport transport, DnsRequestFormat requestFormat, int defaultPort) {
            ulong properties = reader.ReadProperties();
            string address = reader.ReadLengthPrefixedString("address");
            if (!string.IsNullOrEmpty(address)) {
                reader.EnsureAddress(address, allowEmpty: false);
            }

            reader.ReadHashes();
            string host = reader.ReadLengthPrefixedString("hostname");
            if (reader.HasRemaining) {
                reader.ReadBootstrapAddresses();
            }

            string endpointValue = string.IsNullOrWhiteSpace(host) ? address : host;
            if (string.IsNullOrWhiteSpace(endpointValue)) {
                throw new FormatException("DNS stamp hostname or address is required.");
            }

            int? portOverride = !string.IsNullOrWhiteSpace(host) &&
                                !HasExplicitPort(host) &&
                                TryGetExplicitPort(address, out int addressPort)
                ? addressPort
                : null;
            return EndpointFromAddress(endpointValue, transport, requestFormat, defaultPort, properties, portOverride);
        }

        private static DnsResolverEndpoint EndpointFromAddress(string value, Transport transport, DnsRequestFormat requestFormat, int defaultPort, ulong properties, int? portOverride = null) {
            (string host, int port, AddressFamily? family) = SplitHostPort(value, defaultPort);
            return new DnsResolverEndpoint {
                Transport = transport,
                RequestFormat = requestFormat,
                Host = host,
                Port = portOverride ?? port,
                Family = family,
                DnsSecOk = HasDnsSec(properties) ? true : null
            };
        }

        private static (string Host, int Port, AddressFamily? Family) SplitHostPort(string value, int defaultPort) {
            if (string.IsNullOrWhiteSpace(value)) {
                throw new FormatException("Endpoint host is required.");
            }

            string host;
            int port = defaultPort;
            string trimmed = value.Trim();

            if (trimmed.StartsWith("[", StringComparison.Ordinal)) {
                int end = trimmed.IndexOf(']');
                if (end <= 1) {
                    throw new FormatException($"Invalid IPv6 endpoint: {value}");
                }

                host = trimmed.Substring(1, end - 1);
                if (trimmed.Length > end + 1 &&
                    (trimmed[end + 1] != ':' || !int.TryParse(trimmed.Substring(end + 2), out port))) {
                    throw new FormatException($"Invalid port in endpoint: {value}");
                }
            } else {
                int separator = trimmed.LastIndexOf(':');
                if (separator > 0 && trimmed.IndexOf(':') == separator) {
                    host = trimmed.Substring(0, separator);
                    if (!int.TryParse(trimmed.Substring(separator + 1), out port)) {
                        throw new FormatException($"Invalid port in endpoint: {value}");
                    }
                } else {
                    host = trimmed;
                }
            }

            if (port < 1 || port > 65535) {
                throw new FormatException($"Invalid port in endpoint: {value}");
            }

            AddressFamily? family = null;
            if (IPAddress.TryParse(host, out IPAddress? address)) {
                family = address.AddressFamily;
            }

            return (host, port, family);
        }

        private static bool HasExplicitPort(string value) => TryGetExplicitPort(value, out _);

        private static bool TryGetExplicitPort(string value, out int port) {
            port = 0;
            if (string.IsNullOrWhiteSpace(value)) {
                return false;
            }

            string trimmed = value.Trim();
            if (trimmed.StartsWith("[", StringComparison.Ordinal)) {
                int end = trimmed.IndexOf(']');
                return end > 1 &&
                       trimmed.Length > end + 1 &&
                       trimmed[end + 1] == ':' &&
                       int.TryParse(trimmed.Substring(end + 2), out port) &&
                       port >= 1 &&
                       port <= 65535;
            }

            int separator = trimmed.LastIndexOf(':');
            return separator > 0 &&
                   trimmed.IndexOf(':') == separator &&
                   int.TryParse(trimmed.Substring(separator + 1), out port) &&
                   port >= 1 &&
                   port <= 65535;
        }

        private static Uri BuildDohUri(string host, int port, string pathAndQuery) {
            int queryIndex = pathAndQuery.IndexOf('?');
            string path = queryIndex >= 0 ? pathAndQuery.Substring(0, queryIndex) : pathAndQuery;
            string? query = queryIndex >= 0 ? pathAndQuery.Substring(queryIndex + 1) : null;
            var builder = new UriBuilder(Uri.UriSchemeHttps, host, port, path);
            if (query != null) {
                builder.Query = query;
            }

            return builder.Uri;
        }

        private static string BuildAddress(DnsResolverEndpoint endpoint, int defaultPort) {
            if (string.IsNullOrWhiteSpace(endpoint.Host)) {
                throw new ArgumentException("DNS stamp endpoint requires Host.", nameof(endpoint));
            }

            string host = FormatHost(endpoint.Host!);
            string hostForValidation = host.Trim('[', ']');
            if (!IPAddress.TryParse(hostForValidation, out _)) {
                throw new ArgumentException("Plain DNS stamp endpoints require an IP address host.", nameof(endpoint));
            }

            return endpoint.Port > 0 && endpoint.Port != defaultPort
                ? $"{host}:{endpoint.Port}"
                : host;
        }

        private static string BuildHostWithPort(DnsResolverEndpoint endpoint, int defaultPort) {
            if (string.IsNullOrWhiteSpace(endpoint.Host)) {
                throw new ArgumentException("DNS stamp endpoint requires Host.", nameof(endpoint));
            }

            string host = FormatHost(endpoint.Host!);
            return endpoint.Port > 0 && endpoint.Port != defaultPort
                ? $"{host}:{endpoint.Port}"
                : host;
        }

        private static string FormatHost(string host) {
            if (IPAddress.TryParse(host, out IPAddress? address) &&
                address.AddressFamily == AddressFamily.InterNetworkV6 &&
                !host.StartsWith("[", StringComparison.Ordinal) &&
                !host.EndsWith("]", StringComparison.Ordinal)) {
                return $"[{host}]";
            }

            return host;
        }

        private static void WriteDohHostAndPath(Stream stream, DnsResolverEndpoint endpoint) {
            Uri uri = EndpointParser.BuildDohUri(endpoint);
            string host = uri.IsDefaultPort || uri.Port == 443
                ? uri.Host
                : $"{uri.Host}:{uri.Port}";
            string path = string.IsNullOrEmpty(uri.PathAndQuery) ? "/dns-query" : uri.PathAndQuery;
            WriteLengthPrefixed(stream, host);
            WriteLengthPrefixed(stream, path);
        }

        private static void WriteProperties(Stream stream, DnsResolverEndpoint endpoint) {
            ulong properties = endpoint.DnsSecOk == true ? 1UL : 0UL;
            Span<byte> buffer = stackalloc byte[8];
            BinaryPrimitives.WriteUInt64LittleEndian(buffer, properties);
            stream.Write(buffer.ToArray(), 0, buffer.Length);
        }

        private static void WriteLengthPrefixed(Stream stream, string value) {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            if (bytes.Length > byte.MaxValue) {
                throw new ArgumentException("DNS stamp fields cannot exceed 255 bytes.");
            }

            stream.WriteByte((byte)bytes.Length);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static void WriteEmptyVariableLengthSet(Stream stream) {
            stream.WriteByte(0);
        }

        private static bool HasDnsSec(ulong properties) => (properties & 1UL) != 0;

        private static byte[] DecodeBase64Url(string value) {
            string base64 = value.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
            return Convert.FromBase64String(base64);
        }

        private static string EncodeBase64Url(byte[] value) {
            return Convert.ToBase64String(value)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private sealed class StampReader {
            private readonly byte[] _payload;
            private int _offset;

            internal StampReader(byte[] payload) {
                _payload = payload;
            }

            internal bool EndOfPayload => _offset == _payload.Length;

            internal bool HasRemaining => _offset < _payload.Length;

            internal byte ReadByte() {
                EnsureRemaining(1, "byte");
                return _payload[_offset++];
            }

            internal ulong ReadProperties() {
                EnsureRemaining(8, "properties");
                ulong value = BinaryPrimitives.ReadUInt64LittleEndian(_payload.AsSpan(_offset, 8));
                _offset += 8;
                return value;
            }

            internal string ReadLengthPrefixedString(string fieldName) {
                byte[] bytes = ReadLengthPrefixed(fieldName);
                return Encoding.UTF8.GetString(bytes, 0, bytes.Length);
            }

            internal void ReadHashes() {
                if (ReadVariableLengthSet("certificate hash").Any(static hash => hash.Length != 0 && hash.Length != 32)) {
                    throw new FormatException("Certificate hashes in DNS stamps must be 32 bytes.");
                }
            }

            internal void ReadBootstrapAddresses() {
                foreach (byte[] value in ReadVariableLengthSet("bootstrap address")) {
                    if (value.Length == 0) {
                        continue;
                    }

                    string address = Encoding.UTF8.GetString(value, 0, value.Length);
                    EnsureAddress(address, allowEmpty: false);
                }
            }

            internal void EnsureAddress(string value, bool allowEmpty) {
                if (string.IsNullOrEmpty(value)) {
                    if (allowEmpty) {
                        return;
                    }

                    throw new FormatException("DNS stamp address is required.");
                }

                (string host, _, _) = SplitHostPort(value, 53);
                if (!IPAddress.TryParse(host, out _)) {
                    throw new FormatException($"Invalid DNS stamp address: {value}");
                }
            }

            private IEnumerable<byte[]> ReadVariableLengthSet(string fieldName) {
                while (true) {
                    byte lengthByte = ReadByte();
                    bool hasMore = (lengthByte & 0x80) != 0;
                    int length = lengthByte & 0x7F;
                    EnsureRemaining(length, fieldName);

                    byte[] value = new byte[length];
                    Array.Copy(_payload, _offset, value, 0, length);
                    _offset += length;
                    yield return value;

                    if (!hasMore) {
                        break;
                    }
                }
            }

            private byte[] ReadLengthPrefixed(string fieldName) {
                int length = ReadByte();
                EnsureRemaining(length, fieldName);

                byte[] value = new byte[length];
                Array.Copy(_payload, _offset, value, 0, length);
                _offset += length;
                return value;
            }

            private void EnsureRemaining(int length, string fieldName) {
                if (_offset + length > _payload.Length) {
                    throw new FormatException($"DNS stamp has a truncated {fieldName} field.");
                }
            }
        }
    }
}
