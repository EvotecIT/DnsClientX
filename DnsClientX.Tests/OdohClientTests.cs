using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using DnsClientX.Odoh;

namespace DnsClientX.Tests;

/// <summary>Real provider exchange, core query, HTTP privacy and resource lifetime contracts.</summary>
public class OdohClientTests {
    /// <summary>The normal core query workflow parses an authenticated relay response, while concurrent equal queries stay isolated.</summary>
    [Fact]
    public async Task CoreWorkflowAndConcurrentExchangesUseAuthenticatedReplies() {
        using var target = new OdohTestTarget();
        using var http = new HttpClient(target);
        using var provider = new OdohClient(Options(), http);
        using var client = new ClientX(provider.CreateConfiguration());
        DnsResponse response = await client.Resolve("example.com", DnsRecordType.A);
        Assert.Equal("1.2.3.4", Assert.Single(response.Answers).Data);
        Task<byte[]>[] exchanges = Enumerable.Range(0, 24).Select(_ => provider.QueryAsync(OdohTestVectors.Hex(OdohTestVectors.Query))).ToArray();
        byte[][] replies = await Task.WhenAll(exchanges);
        Assert.All(replies, reply => Assert.Equal(0x81, reply[2]));
        Assert.Equal(1, target.ConfigRequests);
        Assert.Equal(25, target.QueryRequests);
        Assert.Equal(25, target.EncryptedQueries.Select(Convert.ToBase64String).Distinct().Count());
        Assert.Equal("relay.example", target.LastRelayUri!.Host);
        Assert.Contains("targethost=target.example%3A8443", target.LastRelayUri.Query);
        Assert.Contains("targetpath=%2Fdns-query", target.LastRelayUri.Query);
    }

    /// <summary>Expired supplied keys and aged/no-store HTTP configurations are never reused on later queries.</summary>
    [Fact]
    public async Task ExpiredConfigurationAndHttpFreshnessCauseReacquisition() {
        using var target = new OdohTestTarget { CacheMaxAge = TimeSpan.FromMinutes(1), Age = TimeSpan.FromMinutes(2) };
        using var http = new HttpClient(target);
        OdohClientOptions options = Options();
        options.InitialConfiguration = OdohTargetConfiguration.Parse(OdohTestVectors.Hex(OdohTestVectors.Config), DateTimeOffset.UtcNow.AddMinutes(-1));
        using var provider = new OdohClient(options, http);
        await provider.QueryAsync(OdohTestVectors.Hex(OdohTestVectors.Query));
        await provider.QueryAsync(OdohTestVectors.Hex(OdohTestVectors.Query));
        Assert.Equal(2, target.ConfigRequests);
        target.NoStore = true;
        target.Age = TimeSpan.Zero;
        await provider.RefreshConfigurationAsync();
        await provider.QueryAsync(OdohTestVectors.Hex(OdohTestVectors.Query));
        Assert.Equal(4, target.ConfigRequests);
    }

    /// <summary>Target key rotation fails the current query and forces the next encrypted exchange to refresh its configuration.</summary>
    [Fact]
    public async Task UnauthorizedKeyDoesNotRetryOrFallBackAndNextQueryRefreshes() {
        using var target = new OdohTestTarget { RejectNextQuery = true };
        using var http = new HttpClient(target);
        using var provider = new OdohClient(Options(), http);
        await Assert.ThrowsAsync<HttpRequestException>(() => provider.QueryAsync(OdohTestVectors.Hex(OdohTestVectors.Query)));
        Assert.Equal(1, target.QueryRequests);
        await provider.QueryAsync(OdohTestVectors.Hex(OdohTestVectors.Query));
        Assert.Equal(2, target.ConfigRequests);
        Assert.Equal(2, target.QueryRequests);
    }

    /// <summary>Cancelling the acquisition releases the gate so a later query can acquire and succeed.</summary>
    [Fact]
    public async Task CanceledConfigurationAcquisitionDoesNotPoisonLaterQueries() {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var target = new OdohTestTarget();
        target.Override = async (_, token) => {
            started.TrySetResult(true);
            await Task.Delay(Timeout.Infinite, token);
            throw new InvalidOperationException();
        };
        using var http = new HttpClient(target);
        using var provider = new OdohClient(Options(), http);
        using var cancellation = new CancellationTokenSource();
        Task<byte[]> exchange = provider.QueryAsync(OdohTestVectors.Hex(OdohTestVectors.Query), cancellation.Token);
        await started.Task;
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => exchange);
        target.Override = null;
        Assert.NotEmpty(await provider.QueryAsync(OdohTestVectors.Hex(OdohTestVectors.Query)));
    }

    /// <summary>Provider disposal cancels its exchanges, preserves caller-owned HTTP lifetime and rejects later work.</summary>
    [Fact]
    public async Task DisposalCancelsExchangeAndHonorsSuppliedClientOwnership() {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var target = new OdohTestTarget();
        target.Override = async (_, token) => {
            started.TrySetResult(true);
            await Task.Delay(Timeout.Infinite, token);
            throw new InvalidOperationException();
        };
        using var http = new HttpClient(target);
        var provider = new OdohClient(Options(), http);
        Task<byte[]> exchange = provider.QueryAsync(OdohTestVectors.Hex(OdohTestVectors.Query));
        await started.Task;
        provider.Dispose();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => exchange);
        Assert.False(target.Disposed);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => provider.QueryAsync(OdohTestVectors.Hex(OdohTestVectors.Query)));
        using var ownedTarget = new OdohTestTarget();
        var owned = new OdohClient(Options(), new HttpClient(ownedTarget), true);
        owned.Dispose();
        Assert.True(ownedTarget.Disposed);
    }

    /// <summary>Bad status, response media type, redirect and authentication cannot produce a usable DNS reply.</summary>
    [Fact]
    public async Task ResponseErrorsAreRejectedWithoutAnotherRequest() {
        using var target = new OdohTestTarget();
        using var http = new HttpClient(target);
        OdohClientOptions options = Options();
        options.InitialConfiguration = OdohCryptographyTests.FixtureConfiguration();
        using var provider = new OdohClient(options, http);
        int calls = 0;
        target.Override = (request, _) => {
            calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                RequestMessage = request,
                Content = new ByteArrayContent(OdohTestVectors.Hex(OdohTestVectors.EncryptedResponse))
            });
        };
        await Assert.ThrowsAsync<InvalidDataException>(() => provider.QueryAsync(OdohTestVectors.Hex(OdohTestVectors.Query)));
        target.Override = (request, _) => {
            calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TemporaryRedirect) { RequestMessage = request });
        };
        await Assert.ThrowsAsync<HttpRequestException>(() => provider.QueryAsync(OdohTestVectors.Hex(OdohTestVectors.Query)));
        target.Override = (_, __) => {
            calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                RequestMessage = new HttpRequestMessage(HttpMethod.Post, "https://target.example/dns-query")
            });
        };
        await Assert.ThrowsAsync<InvalidDataException>(() => provider.QueryAsync(OdohTestVectors.Hex(OdohTestVectors.Query)));
        target.Override = null;
        target.MutateResponse = response => response[response.Length - 1] ^= 1;
        await Assert.ThrowsAsync<CryptographicException>(() => provider.QueryAsync(OdohTestVectors.Hex(OdohTestVectors.Query)));
        Assert.Equal(3, calls);
        Assert.Equal(1, target.QueryRequests);
    }

    /// <summary>Privacy-sensitive input and identifying default headers are rejected before sending requests.</summary>
    [Fact]
    public void InsecureUrisDuplicateRoutingAndIdentifyingDefaultsAreRejected() {
        using var target = new OdohTestTarget();
        using var http = new HttpClient(target);
        Assert.Throws<ArgumentException>(() => new OdohClient(new OdohClientOptions(new Uri("http://relay.example/"), new Uri("https://target.example/")), http));
        Assert.Throws<ArgumentException>(() => new OdohClient(new OdohClientOptions(new Uri("https://relay.example/?targethost=evil"), new Uri("https://target.example/")), http));
        Assert.Throws<ArgumentException>(() => new OdohClient(new OdohClientOptions(new Uri("https://relay.example/"), new Uri("https://target.example/?extra=x")), http));
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "synthetic-fixture");
        Assert.Throws<InvalidOperationException>(() => new OdohClient(Options(), http));
        Assert.Equal(0, target.ConfigRequests);
        Assert.Equal(0, target.QueryRequests);
    }

    /// <summary>Configuration cache honours Expires without Cache-Control and clamps server freshness to the caller's bound.</summary>
    [Fact]
    public async Task ExpiresMetadataAndMaximumConfigurationAgeAreEnforced() {
        using var target = new OdohTestTarget();
        using var http = new HttpClient(target);
        DateTimeOffset date = DateTimeOffset.UtcNow;
        DateTimeOffset expires = date.AddMinutes(5);
        target.Override = (request, _) => {
            var response = new HttpResponseMessage(HttpStatusCode.OK) {
                RequestMessage = request,
                Content = new ByteArrayContent(OdohTestVectors.Hex(OdohTestVectors.Config))
            };
            response.Headers.Date = date;
            response.Content.Headers.Expires = expires;
            return Task.FromResult(response);
        };
        using var provider = new OdohClient(Options(), http);
        Assert.Equal(expires, (await provider.RefreshConfigurationAsync()).ExpiresAt);
        target.Override = null;
        target.CacheMaxAge = TimeSpan.FromDays(100);
        OdohClientOptions options = Options();
        options.MaximumConfigurationAge = TimeSpan.FromMinutes(1);
        using var bounded = new OdohClient(options, http);
        DateTimeOffset before = DateTimeOffset.UtcNow;
        OdohTargetConfiguration configuration = await bounded.RefreshConfigurationAsync();
        DateTimeOffset after = DateTimeOffset.UtcNow;
        Assert.InRange(configuration.ExpiresAt, before.AddMinutes(1), after.AddMinutes(1));
    }

    /// <summary>Protocol limits bound HTTP input before parsing either configurations or response frames.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OversizedHttpBodiesAreRejected(bool configurationAcquisition) {
        using var target = new OdohTestTarget();
        using var http = new HttpClient(target);
        OdohClientOptions options = Options();
        if (!configurationAcquisition) {
            options.InitialConfiguration = OdohCryptographyTests.FixtureConfiguration();
        }
        target.Override = (request, _) => {
            var response = new HttpResponseMessage(HttpStatusCode.OK) {
                RequestMessage = request,
                Content = new ByteArrayContent(new byte[70000])
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/oblivious-dns-message");
            return Task.FromResult(response);
        };
        using var provider = new OdohClient(options, http);
        await Assert.ThrowsAsync<InvalidDataException>(() => provider.QueryAsync(OdohTestVectors.Hex(OdohTestVectors.Query)));
    }

    private static OdohClientOptions Options() {
        return new OdohClientOptions(new Uri("https://relay.example/dns-query?policy=private"), new Uri("https://target.example:8443/dns-query"));
    }
}
