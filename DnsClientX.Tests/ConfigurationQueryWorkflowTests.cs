using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DnsClientX.Odoh;

namespace DnsClientX.Tests;

/// <summary>Reusable supplied-configuration execution preserves transport/security and caller ownership.</summary>
public class ConfigurationQueryWorkflowTests {
    /// <summary>Caller template changes after an await do not change the selected provider or query policy.</summary>
    [Fact]
    public async Task SuppliedConfigurationIsSnapshottedAndProviderRemainsCallerOwned() {
        using var target = new OdohTestTarget();
        using var http = new HttpClient(target);
        using var provider = new OdohClient(new OdohClientOptions(new Uri("https://relay.example/dns-query"), new Uri("https://target.example/dns-query")) {
            InitialConfiguration = OdohCryptographyTests.FixtureConfiguration()
        }, http);
        Configuration configuration = provider.CreateConfiguration();
        configuration.CheckingDisabled = true;
        configuration.TimeOut = 5000;
        configuration.ConnectionAddress = IPAddress.Parse("192.0.2.20");
        byte[]? observed = null;
        target.ObserveQuery = query => observed = query;
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        target.Override = async (request, token) => {
            started.TrySetResult(true);
            await release.Task;
            token.ThrowIfCancellationRequested();
            byte[] query = await request.Content!.ReadAsByteArrayAsync();
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK) { RequestMessage = request, Content = new ByteArrayContent(OdohTestTarget.Reply(query, target.ObserveQuery)) };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/oblivious-dns-message");
            return response;
        };
        var workflow = new ResolveDnsRequest { Names = new[] { "example.com" }, Configuration = configuration, RetryCount = 1 };
        Task<DnsResponse[]> queryTask = ClientX.QueryDns(workflow);
        await started.Task;
        configuration.TimeOut = 1;
        configuration.CheckingDisabled = false;
        configuration.WireTransport = new UnusableTransport();
        release.TrySetResult(true);
        DnsResponse answer = Assert.Single(await queryTask);
        Assert.Equal("1.2.3.4", Assert.Single(answer.Answers).Data);
        Assert.NotNull(observed);
        Assert.NotEqual(0, observed![3] & 0x10);
        Assert.Equal(1, configuration.TimeOut);
        Assert.False(configuration.CheckingDisabled);
        target.Override = null;
        Assert.NotEmpty(await provider.QueryAsync(OdohTestVectors.Hex(OdohTestVectors.Query)));
    }

    /// <summary>Supplied timeout is retained by default and an explicit request override is applied without changing its template.</summary>
    [Fact]
    public async Task SuppliedTimeoutAndExplicitOverrideArePreserved() {
        using var target = new OdohTestTarget();
        using var http = new HttpClient(target);
        using var provider = new OdohClient(new OdohClientOptions(new Uri("https://relay.example/dns-query"), new Uri("https://target.example/dns-query")) {
            InitialConfiguration = OdohCryptographyTests.FixtureConfiguration()
        }, http);
        Configuration configuration = provider.CreateConfiguration();
        configuration.TimeOut = 20;
        target.Override = async (_, token) => {
            await Task.Delay(Timeout.Infinite, token);
            throw new InvalidOperationException();
        };
        Task<DnsResponse[]> timedOut = ClientX.QueryDns(new ResolveDnsRequest {
            Names = new[] { "example.com" }, Configuration = configuration, RetryCount = 1
        });
        // A configured deadline may expire during cold cryptography before HTTP starts.
        // Its observable contract is bounded cancellation, not a handler invocation count.
        Assert.Same(timedOut, await Task.WhenAny(timedOut, Task.Delay(TimeSpan.FromSeconds(1))));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => timedOut);
        // The reply arrives after the supplied 20 ms deadline. An ignored override therefore
        // cancels this query, even on a warm or otherwise fast HTTP path.
        target.Override = async (request, token) => {
            await Task.Delay(80, token).ConfigureAwait(false);
            byte[] query = await request.Content!.ReadAsByteArrayAsync().ConfigureAwait(false);
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK) {
                RequestMessage = request,
                Content = new ByteArrayContent(OdohTestTarget.Reply(query))
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/oblivious-dns-message");
            return response;
        };
        DnsResponse[] answers = await ClientX.QueryDns(new ResolveDnsRequest {
            Names = new[] { "example.com" }, Configuration = configuration, ConfigurationTimeoutMilliseconds = 5000, RetryCount = 1
        });
        Assert.Single(answers);
        Assert.Equal(20, configuration.TimeOut);
    }

    /// <summary>Conflicting resolver sources and invalid configuration timeouts fail before any transport work.</summary>
    [Fact]
    public void ConfigurationSourceValidationPrecedesNetwork() {
        Configuration configuration = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverUDP);
        Assert.Throws<InvalidOperationException>(() => new ResolveDnsRequest {
            Names = new[] { "example.com" }, Configuration = configuration, DnsProviders = new[] { DnsEndpoint.Cloudflare }
        }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResolveDnsRequest {
            Names = new[] { "example.com" }, Configuration = configuration, ConfigurationTimeoutMilliseconds = 0
        }.Validate());
        Assert.Throws<InvalidOperationException>(() => new ResolveDnsRequest {
            Names = new[] { "example.com" }, ConfigurationTimeoutMilliseconds = 2000
        }.Validate());
    }

    /// <summary>Unsupported root iteration never bypasses an installed privacy provider through plaintext DNS.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RootIterationRejectsOptionalProviderBeforeNetwork(bool explicitRootMethod) {
        using var target = new OdohTestTarget();
        using var http = new HttpClient(target);
        using var provider = new OdohClient(new OdohClientOptions(new Uri("https://relay.example/dns-query"), new Uri("https://target.example/dns-query")) {
            InitialConfiguration = OdohCryptographyTests.FixtureConfiguration()
        }, http);
        var configuration = explicitRootMethod ? provider.CreateConfiguration() : new Configuration(DnsEndpoint.RootServer, DnsSelectionStrategy.First);
        configuration.WireTransport = provider;
        using var client = new ClientX(configuration, enableCache: true);
        await Assert.ThrowsAsync<NotSupportedException>(() => explicitRootMethod
            ? client.ResolveFromRoot("example.com") : client.Resolve("example.com", DnsRecordType.A, retryOnTransient: false));
        Assert.Equal(0, target.QueryRequests);
        Assert.Equal(0, target.ConfigRequests);
    }

    private sealed class UnusableTransport : IDnsWireTransport {
        public DnsRequestFormat RequestFormat => DnsRequestFormat.ObliviousDnsOverHttps;
        public Task<DnsWireTransportResponse> QueryAsync(byte[] query, CancellationToken cancellationToken = default) {
            throw new InvalidOperationException("The later template provider must not receive an already-admitted query.");
        }
    }
}
