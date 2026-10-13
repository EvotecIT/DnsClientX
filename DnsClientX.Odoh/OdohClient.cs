using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX.Odoh;

/// <summary>Transports encrypted RFC 9230 DNS messages through an explicitly chosen HTTPS relay.</summary>
/// <remarks>The caller owns this provider's lifetime. Each query uses a fresh ephemeral HPKE context.
/// Configuration acquisition contacts the HTTPS target; DNS queries and replies travel through the relay only.</remarks>
public sealed partial class OdohClient : IDnsWireTransport, IDisposable {
    private const string MessageMediaType = "application/oblivious-dns-message";
    private readonly HttpClient _httpClient;
    private readonly bool _disposeHttpClient;
    private readonly Uri _configurationUri;
    private readonly Uri _relayUri;
    private readonly int _paddingBlockSize;
    private readonly TimeSpan _maximumConfigurationAge;
    private readonly TimeSpan _defaultConfigurationAge;
    private readonly SemaphoreSlim _configurationGate = new SemaphoreSlim(1, 1);
    private readonly CancellationTokenSource _lifetimeCancellation = new CancellationTokenSource();
    private readonly CancellationToken _lifetimeToken;
    private OdohTargetConfiguration? _configuration;
    private int _disposed;

    /// <summary>Creates a client with owned HTTP handling that disables cookies and automatic redirects.</summary>
    /// <param name="options">The relay, target, configuration acquisition, and padding options.</param>
    public OdohClient(OdohClientOptions options) : this(options, CreateHttpClient(options), true) {
    }

    /// <summary>Creates a client using caller-supplied HTTP handling.</summary>
    /// <param name="options">The relay, target, configuration acquisition, and padding options.</param>
    /// <param name="httpClient">An HTTP client whose handler disables cookies, automatic redirects, and default credentials.
    /// It must not inject identifying headers or rewrite request destinations. These opaque handler settings cannot be verified by this provider.</param>
    /// <param name="disposeHttpClient">Whether disposing this provider also disposes the supplied client. The default preserves caller ownership.</param>
    public OdohClient(OdohClientOptions options, HttpClient httpClient, bool disposeHttpClient = false) {
        ValidateOptions(options);
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _configurationUri = options.ConfigurationUri ?? new Uri(new Uri(options.TargetUri.GetLeftPart(UriPartial.Authority)), "/.well-known/odohconfigs");
        _disposeHttpClient = disposeHttpClient;
        _relayUri = CreateRelayUri(options.ProxyUri, options.TargetUri);
        _paddingBlockSize = options.PaddingBlockSize;
        _maximumConfigurationAge = options.MaximumConfigurationAge;
        _defaultConfigurationAge = options.DefaultConfigurationAge;
        _configuration = options.InitialConfiguration;
        _lifetimeToken = _lifetimeCancellation.Token;
        ValidateDefaultHeaders();
    }

    /// <inheritdoc/>
    public DnsRequestFormat RequestFormat => DnsRequestFormat.ObliviousDnsOverHttps;

    /// <inheritdoc/>
    async Task<DnsWireTransportResponse> IDnsWireTransport.QueryAsync(byte[] query, CancellationToken cancellationToken) {
        byte[] message = await QueryAsync(query, cancellationToken).ConfigureAwait(false);
        return new DnsWireTransportResponse(message, Transport.Doh);
    }

    /// <summary>Creates a core configuration that delegates DNS wire exchanges to this provider.</summary>
    /// <remarks>Disposing the core client does not dispose this provider.</remarks>
    public Configuration CreateConfiguration() {
        ThrowIfDisposed();
        return new Configuration(_relayUri, RequestFormat) {
            Port = _relayUri.Port,
            WireTransport = this
        };
    }

    /// <inheritdoc/>
    public async Task<byte[]> QueryAsync(byte[] query, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (query == null) {
            throw new ArgumentNullException(nameof(query));
        }
        if (query.Length < 12 || query.Length > 65483) {
            throw new ArgumentException("A DNS wire query must include a 12-byte header and fit within the ODoH encrypted message limit.", nameof(query));
        }
        // Snapshot before awaiting acquisition so a caller's buffer cannot change the response binding.
        byte[] snapshot = (byte[])query.Clone();
        using (var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeToken)) {
            CancellationToken token = cancellation.Token;
            try {
                token.ThrowIfCancellationRequested();
                OdohTargetConfiguration configuration = await GetConfigurationAsync(false, token).ConfigureAwait(false);
                using (OdohQueryContext context = OdohQueryContext.Create(configuration, snapshot, _paddingBlockSize)) {
                    token.ThrowIfCancellationRequested();
                    ValidateDefaultHeaders();
                    using (var request = new HttpRequestMessage(HttpMethod.Post, _relayUri)) {
                        request.Content = new ByteArrayContent(context.Message);
                        request.Content.Headers.ContentType = new MediaTypeHeaderValue(MessageMediaType);
                        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(MessageMediaType));
                        request.Headers.CacheControl = new CacheControlHeaderValue { NoStore = true };
                        using (HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false)) {
                            ValidateResponseDestination(response, _relayUri);
                            if (response.StatusCode == HttpStatusCode.Unauthorized) {
                                // Key rotation may precede HTTP expiry. Fail this exchange and reacquire on the next query.
                                Interlocked.CompareExchange(ref _configuration, null, configuration);
                            }
                            response.EnsureSuccessStatusCode();
                            if (!string.Equals(response.Content?.Headers.ContentType?.MediaType, MessageMediaType, StringComparison.OrdinalIgnoreCase)) {
                                throw new InvalidDataException("The relay did not return application/oblivious-dns-message.");
                            }
                            byte[] encryptedResponse = await ReadBoundedAsync(response.Content!, 65556, token).ConfigureAwait(false);
                            token.ThrowIfCancellationRequested();
                            return context.OpenResponse(encryptedResponse);
                        }
                    }
                }
            } finally {
                HpkeX25519.Clear(snapshot);
            }
        }
    }

    /// <summary>Reacquires target keys over HTTPS and returns the first supported configuration.</summary>
    public async Task<OdohTargetConfiguration> RefreshConfigurationAsync(CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        using (var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeToken)) {
            return await GetConfigurationAsync(true, cancellation.Token).ConfigureAwait(false);
        }
    }

    /// <summary>Cancels outstanding exchanges and releases owned HTTP resources.</summary>
    public void Dispose() {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) {
            return;
        }
        _lifetimeCancellation.Cancel();
        if (_disposeHttpClient) {
            _httpClient.Dispose();
        }
        // Leave the gate usable by in-flight finally blocks. It owns no native handle unless AvailableWaitHandle is requested.
        _lifetimeCancellation.Dispose();
    }

    private void ThrowIfDisposed() {
        if (Volatile.Read(ref _disposed) != 0) {
            throw new ObjectDisposedException(nameof(OdohClient));
        }
    }

    private static void ValidateOptions(OdohClientOptions options) {
        if (options == null) {
            throw new ArgumentNullException(nameof(options));
        }
        ValidateEndpoint(options.ProxyUri, nameof(options.ProxyUri), false);
        ValidateEndpoint(options.TargetUri, nameof(options.TargetUri), true);
        if (options.ConfigurationUri != null) {
            ValidateEndpoint(options.ConfigurationUri, nameof(options.ConfigurationUri), false);
        }
        if (options.PaddingBlockSize < 0 || options.PaddingBlockSize > 65535) {
            throw new ArgumentOutOfRangeException(nameof(options.PaddingBlockSize));
        }
        if (options.MaximumConfigurationAge <= TimeSpan.Zero || options.DefaultConfigurationAge < TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(options.MaximumConfigurationAge), "Configuration lifetimes must be positive (the default lifetime may be zero).");
        }
        CreateRelayUri(options.ProxyUri, options.TargetUri);
    }

    private static HttpClient CreateHttpClient(OdohClientOptions options) {
        ValidateOptions(options);
        return new HttpClient(new HttpClientHandler {
            AllowAutoRedirect = false,
            UseCookies = false,
            UseDefaultCredentials = false
        });
    }

    private void ValidateDefaultHeaders() {
        foreach (var header in _httpClient.DefaultRequestHeaders) {
            if (!string.Equals(header.Key, "Accept", StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException("An ODoH HTTP client must not carry default request headers other than Accept.");
            }
        }
    }

    private static void ValidateResponseDestination(HttpResponseMessage response, Uri expected) {
        Uri? actual = response.RequestMessage?.RequestUri;
        if (actual != null && actual != expected) {
            throw new InvalidDataException("The HTTP handler changed the ODoH request destination. Automatic redirects must be disabled.");
        }
    }

    private static void ValidateEndpoint(Uri uri, string parameterName, bool requirePathOnly) {
        if (!uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0 || (requirePathOnly && uri.Query.Length != 0)) {
            throw new ArgumentException("An ODoH endpoint must be an absolute HTTPS URI without credentials or a fragment; the target must not have a query.", parameterName);
        }
    }

    private static Uri CreateRelayUri(Uri proxyUri, Uri targetUri) {
        string existing = proxyUri.Query.TrimStart('?');
        foreach (string pair in existing.Split('&')) {
            string key = Uri.UnescapeDataString(pair.Split('=')[0]);
            if (string.Equals(key, "targethost", StringComparison.OrdinalIgnoreCase) || string.Equals(key, "targetpath", StringComparison.OrdinalIgnoreCase)) {
                throw new ArgumentException("The relay URI must not contain targethost or targetpath parameters.", nameof(proxyUri));
            }
        }
        var builder = new UriBuilder(proxyUri) {
            Query = (existing.Length == 0 ? "" : existing + "&") + "targethost=" + Uri.EscapeDataString(targetUri.Authority) + "&targetpath=" + Uri.EscapeDataString(targetUri.AbsolutePath)
        };
        return builder.Uri;
    }

    private static async Task<byte[]> ReadBoundedAsync(HttpContent content, int maximumLength, CancellationToken cancellationToken) {
        if (content.Headers.ContentLength > maximumLength) {
            throw new InvalidDataException("The ODoH HTTP response exceeds its protocol limit.");
        }
        using (Stream stream = await content.ReadAsStreamAsync().ConfigureAwait(false)) {
            using (var output = new MemoryStream()) {
                var buffer = new byte[4096];
                while (true) {
                    int count = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                    if (count == 0) {
                        return output.ToArray();
                    }
                    if (output.Length + count > maximumLength) {
                        throw new InvalidDataException("The ODoH HTTP response exceeds its protocol limit.");
                    }
                    output.Write(buffer, 0, count);
                }
            }
        }
    }
}
