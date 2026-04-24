using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    /// Parses user-provided resolver endpoint strings into validated endpoints.
    /// </summary>
    public static class EndpointParser {
        private const int MaxImportedContentBytes = 256 * 1024;
        private static readonly TimeSpan ImportHttpTimeout = TimeSpan.FromSeconds(15);

        /// <summary>
        /// Loads resolver endpoint input values from inline entries, files, and URLs.
        /// </summary>
        /// <param name="inputs">Inline resolver endpoint values such as <c>udp@1.1.1.1:53</c> or <c>doh3@https://dns.quad9.net/dns-query</c>.</param>
        /// <param name="files">Text files containing one resolver endpoint per line.</param>
        /// <param name="urls">HTTP or HTTPS URLs that return resolver endpoint text content.</param>
        /// <param name="cancellationToken">Cancellation token used for file and network loading.</param>
        /// <returns>A de-duplicated array of loaded endpoint strings.</returns>
        public static async Task<string[]> LoadInputsAsync(
            IEnumerable<string>? inputs = null,
            IEnumerable<string>? files = null,
            IEnumerable<string>? urls = null,
            CancellationToken cancellationToken = default) {
            var list = new List<string>();

            foreach (string? input in inputs ?? Array.Empty<string>()) {
                string trimmed = input?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(trimmed)) {
                    list.Add(trimmed);
                }
            }

            foreach (string? fileEntry in files ?? Array.Empty<string>()) {
                cancellationToken.ThrowIfCancellationRequested();

                string filePath = fileEntry?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(filePath)) {
                    continue;
                }

                string fullPath = Path.GetFullPath(filePath);
                if (!File.Exists(fullPath)) {
                    throw new FileNotFoundException($"Resolver file not found: {fileEntry}", fileEntry);
                }

                FileInfo fileInfo = new FileInfo(fullPath);
                if (fileInfo.Length > MaxImportedContentBytes) {
                    throw new InvalidOperationException($"Resolver file exceeds the {MaxImportedContentBytes} byte import limit: {fileEntry}");
                }

                using var reader = File.OpenText(fullPath);
                string content = await reader.ReadToEndAsync().ConfigureAwait(false);
                list.AddRange(ParseImportedEntries(content));
            }

            string[] urlEntries = (urls ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .ToArray();

            if (urlEntries.Length > 0) {
                using var httpClient = new HttpClient {
                    Timeout = ImportHttpTimeout
                };
                foreach (string urlEntry in urlEntries) {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!Uri.TryCreate(urlEntry, UriKind.Absolute, out Uri? uri) ||
                        (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) {
                        throw new ArgumentException($"Invalid resolver URL: {urlEntry}", nameof(urls));
                    }

                    using HttpResponseMessage response = await httpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode) {
                        throw new InvalidOperationException($"Resolver URL returned HTTP {(int)response.StatusCode}: {urlEntry}");
                    }

                    string content = await ReadContentWithLimitAsync(response, cancellationToken).ConfigureAwait(false);
                    list.AddRange(ParseImportedEntries(content));
                }
            }

            return list
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        /// <summary>
        /// Loads resolver endpoint inputs and parses them into validated endpoints.
        /// </summary>
        /// <param name="inputs">Inline resolver endpoint values.</param>
        /// <param name="files">Resolver endpoint file paths.</param>
        /// <param name="urls">Resolver endpoint URLs.</param>
        /// <param name="cancellationToken">Cancellation token used for file and network loading.</param>
        /// <returns>The parsed endpoints together with non-fatal parsing errors.</returns>
        public static async Task<(DnsResolverEndpoint[] Endpoints, IReadOnlyList<string> Errors)> TryParseManyAsync(
            IEnumerable<string>? inputs = null,
            IEnumerable<string>? files = null,
            IEnumerable<string>? urls = null,
            CancellationToken cancellationToken = default) {
            string[] loadedInputs = await LoadInputsAsync(inputs, files, urls, cancellationToken).ConfigureAwait(false);
            DnsResolverEndpoint[] endpoints = TryParseMany(loadedInputs, out IReadOnlyList<string> errors);
            return (endpoints, errors);
        }

        /// <summary>
        /// Validates resolver endpoint inputs from inline entries, files, and URLs while preserving source context.
        /// </summary>
        /// <param name="inputs">Inline resolver endpoint values.</param>
        /// <param name="files">Text files containing resolver endpoint entries.</param>
        /// <param name="urls">HTTP or HTTPS URLs containing resolver endpoint entries.</param>
        /// <param name="cancellationToken">Cancellation token used for file and network loading.</param>
        /// <returns>Validation results for every discovered entry.</returns>
        public static async Task<ResolverEndpointValidationResult[]> ValidateManyAsync(
            IEnumerable<string>? inputs = null,
            IEnumerable<string>? files = null,
            IEnumerable<string>? urls = null,
            CancellationToken cancellationToken = default) {
            var results = new List<ResolverEndpointValidationResult>();

            foreach (string trimmed in (inputs ?? Array.Empty<string>())
                .Select(static input => input?.Trim() ?? string.Empty)
                .Where(static trimmed => !string.IsNullOrWhiteSpace(trimmed))) {
                results.Add(ValidateEntry(trimmed, "inline", null));
            }

            foreach (string? fileEntry in files ?? Array.Empty<string>()) {
                cancellationToken.ThrowIfCancellationRequested();
                string filePath = fileEntry?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(filePath)) {
                    continue;
                }

                string fullPath;
                try {
                    fullPath = Path.GetFullPath(filePath);
                } catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException || ex is IOException) {
                    results.Add(new ResolverEndpointValidationResult {
                        Source = fileEntry ?? string.Empty,
                        Entry = fileEntry ?? string.Empty,
                        IsValid = false,
                        Error = ex.Message
                    });
                    continue;
                }

                if (!File.Exists(fullPath)) {
                    results.Add(new ResolverEndpointValidationResult {
                        Source = fileEntry ?? string.Empty,
                        Entry = fileEntry ?? string.Empty,
                        IsValid = false,
                        Error = $"Resolver file not found: {fileEntry}"
                    });
                    continue;
                }

                FileInfo fileInfo = new FileInfo(fullPath);
                if (fileInfo.Length > MaxImportedContentBytes) {
                    results.Add(new ResolverEndpointValidationResult {
                        Source = fullPath,
                        Entry = fileEntry ?? string.Empty,
                        IsValid = false,
                        Error = $"Resolver file exceeds the {MaxImportedContentBytes} byte import limit: {fileEntry}"
                    });
                    continue;
                }

                try {
                    using var reader = File.OpenText(fullPath);
                    string content = await reader.ReadToEndAsync().ConfigureAwait(false);
                    results.AddRange(ValidateImportedContent(content, fullPath));
                } catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) {
                    results.Add(new ResolverEndpointValidationResult {
                        Source = fullPath,
                        Entry = fileEntry ?? string.Empty,
                        IsValid = false,
                        Error = ex.Message
                    });
                }
            }

            string[] urlEntries = (urls ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .ToArray();

            if (urlEntries.Length > 0) {
                using var httpClient = new HttpClient {
                    Timeout = ImportHttpTimeout
                };

                foreach (string urlEntry in urlEntries) {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!Uri.TryCreate(urlEntry, UriKind.Absolute, out Uri? uri) ||
                        (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) {
                        results.Add(new ResolverEndpointValidationResult {
                            Source = urlEntry,
                            Entry = urlEntry,
                            IsValid = false,
                            Error = $"Invalid resolver URL: {urlEntry}"
                        });
                        continue;
                    }

                    try {
                        using HttpResponseMessage response = await httpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false);
                        if (!response.IsSuccessStatusCode) {
                            results.Add(new ResolverEndpointValidationResult {
                                Source = urlEntry,
                                Entry = urlEntry,
                                IsValid = false,
                                Error = $"Resolver URL returned HTTP {(int)response.StatusCode}: {urlEntry}"
                            });
                            continue;
                        }

                        string content = await ReadContentWithLimitAsync(response, cancellationToken).ConfigureAwait(false);
                        results.AddRange(ValidateImportedContent(content, urlEntry));
                    } catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested) {
                        throw;
                    } catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is InvalidOperationException) {
                        results.Add(new ResolverEndpointValidationResult {
                            Source = urlEntry,
                            Entry = urlEntry,
                            IsValid = false,
                            Error = ex.Message
                        });
                    }
                }
            }

            return results.ToArray();
        }

        /// <summary>
        /// Parses imported resolver endpoint content, skipping blank lines and full-line comments.
        /// </summary>
        /// <param name="content">Imported text content.</param>
        /// <returns>Parsed endpoint strings.</returns>
        public static IEnumerable<string> ParseImportedEntries(string? content) {
            if (string.IsNullOrWhiteSpace(content)) {
                yield break;
            }

            using var reader = new StringReader(content);
            while (reader.ReadLine() is string line) {
                string trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) ||
                    trimmed.StartsWith("#", StringComparison.Ordinal) ||
                    trimmed.StartsWith(";", StringComparison.Ordinal) ||
                    trimmed.StartsWith("//", StringComparison.Ordinal)) {
                    continue;
                }

                foreach (string entry in trimmed.Split(',')) {
                    string value = entry.Trim();
                    if (!string.IsNullOrWhiteSpace(value)) {
                        yield return value;
                    }
                }
            }
        }

        private static IEnumerable<ResolverEndpointValidationResult> ValidateImportedContent(string? content, string source) {
            if (string.IsNullOrWhiteSpace(content)) {
                yield break;
            }

            using var reader = new StringReader(content);
            int lineNumber = 0;
            while (reader.ReadLine() is string line) {
                lineNumber++;
                string trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) ||
                    trimmed.StartsWith("#", StringComparison.Ordinal) ||
                    trimmed.StartsWith(";", StringComparison.Ordinal) ||
                    trimmed.StartsWith("//", StringComparison.Ordinal)) {
                    continue;
                }

                foreach (string value in trimmed.Split(',')
                    .Select(static entry => entry.Trim())
                    .Where(static value => !string.IsNullOrWhiteSpace(value))) {
                    yield return ValidateEntry(value, source, lineNumber);
                }
            }
        }

        private static ResolverEndpointValidationResult ValidateEntry(string value, string source, int? lineNumber) {
            DnsResolverEndpoint[] endpoints = TryParseMany(new[] { value }, out IReadOnlyList<string> errors);
            if (endpoints.Length == 1 && errors.Count == 0) {
                return new ResolverEndpointValidationResult {
                    Source = source,
                    LineNumber = lineNumber,
                    Entry = value,
                    IsValid = true,
                    Endpoint = endpoints[0]
                };
            }

            return new ResolverEndpointValidationResult {
                Source = source,
                LineNumber = lineNumber,
                Entry = value,
                IsValid = false,
                Error = errors.Count == 0 ? "Endpoint could not be parsed." : string.Join("; ", errors)
            };
        }

        /// <summary>
        /// Tries to parse multiple endpoint input strings.
        /// Accepted formats:
        ///  - IPv4: "1.1.1.1:53"
        ///  - IPv6: "[2606:4700:4700::1111]:53"
        ///  - Hostname: "dns.google:53"
        ///  - DoH URL: "https://dns.google/dns-query"
        ///  - Explicit transport: "tcp@1.1.1.1:53", "dot@dns.google:853", "doh@https://dns.google/dns-query"
        ///  - Modern transport shortcuts: "doq@dns.quad9.net:853", "doh3@https://dns.quad9.net/dns-query"
        /// </summary>
        /// <param name="inputs">Endpoint input values to parse.</param>
        /// <param name="errors">Receives parsing errors for entries that could not be normalized.</param>
        /// <returns>Successfully parsed resolver endpoints.</returns>
        public static DnsResolverEndpoint[] TryParseMany(IEnumerable<string> inputs, out IReadOnlyList<string> errors) {
            var list = new List<DnsResolverEndpoint>();
            var errs = new List<string>();

            foreach (var rawIn in inputs ?? Array.Empty<string>()) {
                string raw = rawIn?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(raw)) {
                    errs.Add("Empty endpoint string");
                    continue;
                }
                if (raw.Any(char.IsWhiteSpace)) {
                    errs.Add($"Endpoint contains whitespace: {rawIn}");
                    continue;
                }

                if (DnsStamp.IsStamp(raw)) {
                    if (DnsStamp.TryParse(raw, out DnsResolverEndpoint? stampEndpoint, out string? stampError)) {
                        list.Add(stampEndpoint!);
                    } else {
                        errs.Add($"Invalid DNS stamp: {stampError}");
                    }

                    continue;
                }

                Transport? explicitTransport = null;
                DnsRequestFormat? explicitRequestFormat = null;
                int transportSeparator = raw.IndexOf('@');
                if (transportSeparator > 0) {
                    string transportName = raw.Substring(0, transportSeparator);
                    if (!TryParseTransportPrefix(transportName, out Transport parsedTransport, out DnsRequestFormat? parsedRequestFormat)) {
                        errs.Add($"Unsupported transport prefix: {transportName}");
                        continue;
                    }

                    explicitTransport = parsedTransport;
                    explicitRequestFormat = parsedRequestFormat;
                    raw = raw.Substring(transportSeparator + 1);
                    if (string.IsNullOrWhiteSpace(raw)) {
                        errs.Add($"Missing endpoint after transport prefix: {rawIn}");
                        continue;
                    }
                }

                // DoH URL
                if (raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) {
                    if (Uri.TryCreate(raw, UriKind.Absolute, out var uri)) {
                        if (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase)) {
                            errs.Add($"Unsupported scheme for DoH: {raw}");
                            continue;
                        }
                        if (explicitTransport.HasValue && explicitTransport.Value != Transport.Doh) {
                            errs.Add($"Transport {explicitTransport.Value} does not support HTTPS URL endpoints: {rawIn}");
                            continue;
                        }
                        list.Add(new DnsResolverEndpoint {
                            Transport = Transport.Doh,
                            DohUrl = uri,
                            Host = uri.Host,
                            Port = uri.IsDefaultPort ? 443 : uri.Port,
                            RequestFormat = explicitRequestFormat
                        });
                        continue;
                    }
                    errs.Add($"Invalid DoH URL: {raw}");
                    continue;
                }

                // IPv6 in brackets
                if (raw.StartsWith("[")) {
                    int end = raw.IndexOf(']');
                    if (end > 1) {
                        string ip = raw.Substring(1, end - 1);
                        string portPart = raw.Length > end + 1 && raw[end + 1] == ':' ? raw.Substring(end + 2) : GetDefaultPort(explicitTransport).ToString();
                        if (IPAddress.TryParse(ip, out var addr) && addr.AddressFamily == AddressFamily.InterNetworkV6 && int.TryParse(portPart, out int p) && p > 0 && p <= 65535) {
                            list.Add(new DnsResolverEndpoint {
                                Host = ip,
                                Port = p,
                                Transport = explicitTransport ?? Transport.Udp,
                                Family = AddressFamily.InterNetworkV6,
                                RequestFormat = explicitRequestFormat
                            });
                            continue;
                        }
                    }
                    errs.Add($"Invalid IPv6 endpoint: {raw}");
                    continue;
                }

                // Split host:port (IPv4 or hostname)
                var parts = raw.Split(':');
                if (parts.Length == 2) {
                    if (int.TryParse(parts[1], out int port) && port > 0 && port <= 65535) {
                        string host = parts[0];
                        AddressFamily? family = null;
                        if (IPAddress.TryParse(host, out var ipAddr)) {
                            family = ipAddr.AddressFamily;
                        }
                        list.Add(new DnsResolverEndpoint {
                            Host = host,
                            Port = port,
                            Transport = explicitTransport ?? Transport.Udp,
                            Family = family,
                            RequestFormat = explicitRequestFormat
                        });
                        continue;
                    } else {
                        errs.Add($"Invalid port in endpoint: {raw}");
                        continue;
                    }
                }

                // Plain host with default port
                if (!string.IsNullOrWhiteSpace(raw)) {
                    AddressFamily? family = null;
                    if (IPAddress.TryParse(raw, out var ipAddr)) {
                        family = ipAddr.AddressFamily;
                    }
                    list.Add(new DnsResolverEndpoint {
                        Host = raw,
                        Port = GetDefaultPort(explicitTransport),
                        Transport = explicitTransport ?? Transport.Udp,
                        Family = family,
                        RequestFormat = explicitRequestFormat
                    });
                    continue;
                }

                errs.Add($"Unrecognized endpoint format: {raw}");
            }

            errors = errs;
            return list.ToArray();
        }

        /// <summary>
        /// Builds the effective DoH URI for a parsed endpoint, preserving custom ports.
        /// </summary>
        /// <param name="endpoint">The parsed resolver endpoint.</param>
        /// <returns>The effective DoH URI.</returns>
        public static Uri BuildDohUri(DnsResolverEndpoint endpoint) {
            if (endpoint == null) {
                throw new ArgumentNullException(nameof(endpoint));
            }

            if (endpoint.DohUrl != null) {
                return endpoint.DohUrl;
            }

            if (string.IsNullOrWhiteSpace(endpoint.Host)) {
                throw new ArgumentException("DoH endpoint requires Host.", nameof(endpoint));
            }

            var builder = new UriBuilder(Uri.UriSchemeHttps, endpoint.Host!) {
                Path = "/dns-query"
            };

            if (endpoint.Port > 0 && endpoint.Port != 443) {
                builder.Port = endpoint.Port;
            }

            return builder.Uri;
        }

        private static bool TryParseTransportPrefix(string value, out Transport transport, out DnsRequestFormat? requestFormat) {
            switch (value.Trim().ToLowerInvariant()) {
                case "udp":
                    transport = Transport.Udp;
                    requestFormat = DnsRequestFormat.DnsOverUDP;
                    return true;
                case "tcp":
                    transport = Transport.Tcp;
                    requestFormat = DnsRequestFormat.DnsOverTCP;
                    return true;
                case "dot":
                case "tls":
                    transport = Transport.Dot;
                    requestFormat = DnsRequestFormat.DnsOverTLS;
                    return true;
                case "doh":
                case "https":
                    transport = Transport.Doh;
                    requestFormat = DnsRequestFormat.DnsOverHttps;
                    return true;
                case "doh3":
                case "http3":
                case "h3":
                    transport = Transport.Doh;
                    requestFormat = DnsRequestFormat.DnsOverHttp3;
                    return true;
                case "quic":
                case "doq":
                    transport = Transport.Quic;
                    requestFormat = DnsRequestFormat.DnsOverQuic;
                    return true;
                case "grpc":
                    transport = Transport.Grpc;
                    requestFormat = DnsRequestFormat.DnsOverGrpc;
                    return true;
                case "multicast":
                case "mdns":
                    transport = Transport.Multicast;
                    requestFormat = DnsRequestFormat.Multicast;
                    return true;
                default:
                    transport = default;
                    requestFormat = null;
                    return false;
            }
        }

        private static int GetDefaultPort(Transport? transport) {
            return transport switch {
                Transport.Dot => 853,
                Transport.Quic => 853,
                Transport.Grpc => 443,
                Transport.Doh => 443,
                Transport.Multicast => 5353,
                _ => 53
            };
        }

        private static async Task<string> ReadContentWithLimitAsync(HttpResponseMessage response, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            using Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var reader = new StreamReader(stream);
            char[] buffer = new char[4096];
            int totalChars = 0;
            var content = new System.Text.StringBuilder();

            while (true) {
                cancellationToken.ThrowIfCancellationRequested();
                int read = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                if (read == 0) {
                    break;
                }

                totalChars += read;
                if (totalChars > MaxImportedContentBytes) {
                    throw new InvalidOperationException($"Resolver URL content exceeds the {MaxImportedContentBytes} byte import limit: {response.RequestMessage?.RequestUri}");
                }

                content.Append(buffer, 0, read);
            }

            return content.ToString();
        }
    }
}
