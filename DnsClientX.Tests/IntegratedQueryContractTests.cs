using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX.Tests;

/// <summary>Protects the request, stream, metadata and authenticated-cache contracts where optional transports meet reusable queries.</summary>
[Collection("NoParallel")]
public class IntegratedQueryContractTests {
    /// <summary>Streaming uses the explicit request limit, then the frozen configuration limit, then its bounded default.</summary>
    [Theory]
    [InlineData(1, 0, 1)]
    [InlineData(1, 2, 2)]
    [InlineData(2, 1, 1)]
    [InlineData(null, 0, 3)]
    public async Task ConfiguredStreamRetainsConcurrencyPrecedence(int? configuredLimit, int requestLimit, int expectedPeak) {
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        object counters = new();
        int active = 0, peak = 0;
        using var provider = new TestProvider(async (query, token) => {
            lock (counters) {
                active++;
                peak = Math.Max(peak, active);
                if (active >= expectedPeak) started.TrySetResult(true);
            }
            try {
                var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                using CancellationTokenRegistration registration = token.Register(() => canceled.TrySetCanceled());
                await Task.WhenAny(release.Task, canceled.Task);
                token.ThrowIfCancellationRequested();
                return AddressReply(query);
            } finally {
                lock (counters) active--;
            }
        });
        Configuration configuration = ProviderConfiguration(provider);
        configuration.MaxConcurrency = configuredLimit;
        var request = new ResolveDnsRequest {
            Names = new[] { "one.example.com", "two.example.com", "three.example.com" },
            Configuration = configuration, RetryCount = 1, MaxConcurrency = requestLimit
        };
        await using IAsyncEnumerator<DnsResponse> enumerator = ClientX.QueryDnsStreaming(request).GetAsyncEnumerator();
        Task<bool>? first = null;
        try {
            first = enumerator.MoveNextAsync().AsTask();
            Assert.Same(started.Task, await Task.WhenAny(started.Task, Task.Delay(TimeSpan.FromSeconds(2))));
            Assert.Equal(expectedPeak, provider.QueryCount);
            Assert.False(first.IsCompleted);
            configuration.MaxConcurrency = 3;
            request.MaxConcurrency = 3;
            release.TrySetResult(true);
            Assert.True(await first);
            int answers = 1;
            while (await enumerator.MoveNextAsync()) answers++;
            Assert.Equal(3, answers);
            Assert.Equal(expectedPeak, peak);
            Assert.Equal(0, active);
            Assert.False(provider.Disposed);
        } finally {
            release.TrySetResult(true);
            if (first != null) await first;
        }
    }

    /// <summary>Both request surfaces retain the supplied provider, EDNS options, typed metadata and explicit cache policy.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuppliedProviderRetainsRequestOptionsAndCachePolicy(bool streaming) {
        int fallbackAttempts = 0;
        SystemInformation.SetDnsServerProvider(() => {
            fallbackAttempts++;
            throw new InvalidOperationException("A supplied privacy provider must not select System DNS.");
        });
        byte[]? observed = null;
        using var provider = new TestProvider((query, _) => {
            observed = query;
            return Task.FromResult(AddressReply(query));
        });
        string name = "cache-" + Guid.NewGuid().ToString("N") + ".example.com";
        Configuration configuration = ProviderConfiguration(provider);
        configuration.CheckingDisabled = true;
        var request = new ResolveDnsRequest {
            Names = new[] { name, name }, Configuration = configuration,
            RetryCount = 1, MaxConcurrency = 1, TypedRecords = true,
            RequestNsid = true, EdnsCookie = new byte[8], EdnsPaddingLength = 16,
            ResponseCache = true, MaxCacheTtlSeconds = 1
        };
        try {
            var responses = new List<DnsResponse>();
            if (streaming) {
                await foreach (DnsResponse response in ClientX.QueryDnsStreaming(request)) responses.Add(response);
            } else {
                responses.AddRange(await ClientX.QueryDns(request));
            }
            Assert.Equal(2, responses.Count);
            Assert.Equal(1, provider.QueryCount);
            Assert.Equal(0, fallbackAttempts);
            Assert.Equal(DnsResponseSource.Network, responses[0].ResponseSource);
            Assert.Equal(DnsResponseSource.Cache, responses[1].ResponseSource);
            Assert.All(responses, response => {
                Assert.Equal(Transport.Tcp, response.UsedTransport);
                Assert.Equal(DnsRequestFormat.DnsCrypt, Assert.Single(response.Questions).RequestFormat);
                DnsTypedRecord<object> typed = Assert.Single(response.GetTypedAnswers<object>());
                Assert.Equal(DnsWireNameCodec.Canonical(name), DnsWireNameCodec.Canonical(typed.Name));
                Assert.Equal((ushort)1, typed.Class);
                Assert.NotEmpty(Assert.Single(response.Answers).RawRdata!);
                Assert.NotEmpty(response.TypedAnswers!);
            });
            Assert.InRange(Assert.Single(responses[1].Answers).TTL, 0, Assert.Single(responses[0].Answers).TTL);
            Assert.NotNull(observed);
            Assert.True(DnsWireMessageParser.TryParseHeader(observed, out DnsWireHeaderInfo header));
            Assert.True(header.CheckingDisabled);
            foreach (ushort optionCode in new ushort[] { 3, 10, 12 }) {
                Assert.True(DnsWireMessageParser.TryContainsEdnsOption(observed, optionCode, out bool contains));
                Assert.True(contains);
            }
            Assert.False(provider.Disposed);

            await Task.Delay(1100);
            request.Names = new[] { name };
            DnsResponse refreshed = Assert.Single(await ClientX.QueryDns(request));
            Assert.Equal(DnsResponseSource.Network, refreshed.ResponseSource);
            Assert.Equal(2, provider.QueryCount);

            request.ResponseCache = false;
            request.Names = new[] { name, name };
            await ClientX.QueryDns(request);
            Assert.Equal(4, provider.QueryCount);
            Assert.True(configuration.CheckingDisabled);
            Assert.Equal(5000, configuration.TimeOut);
        } finally {
            SystemInformation.SetDnsServerProvider(null);
        }
    }

    /// <summary>Private island anchors authenticate optional-provider answers, partition material reuse and bound each proof's lifetime.</summary>
    [Fact]
    public async Task PrivateAnchorPartitionsMaterialCacheAcrossOptionsAndStreaming() {
        using var fixture = new DnsSecSignedFixture();
        int keyQueries = 0;
        using var provider = new TestProvider(async (query, token) => {
            var reader = new DnsWireReader(query, 12);
            string name = reader.ReadName();
            var type = (DnsRecordType)reader.ReadUInt16();
            reader.ReadUInt16();
            DnsResponse response;
            if (type == DnsRecordType.DNSKEY && DnsWireNameCodec.Canonical(name) == fixture.Zone.Name) {
                keyQueries++;
                response = await fixture.LookupAsync(name, type, token);
            } else {
                Assert.Equal(DnsRecordType.A, type);
                response = fixture.Signed(name, type, new byte[] { 192, 0, 2, 9 });
            }
            return new DnsWireTransportResponse(DnsSecSignedFixture.SerializeResponse(query, reader.Position, response), Transport.Tcp);
        });
        DnsSecZoneTrustAnchor anchor = Anchor(fixture, fixture.Now.AddSeconds(30));
        Configuration configuration = ProviderConfiguration(provider);
        configuration.DnsSecTrustAnchors = new[] { anchor };
        configuration.MaxConcurrency = 1;
        using var client = new ClientX(configuration, enableCache: true);
        var options = new DnsResolveOptions { ValidateDnsSec = true, RetryOnTransient = false, TypedRecords = true };
        var answers = new List<DnsResponse>();
        await foreach (DnsResponse answer in client.ResolveStreaming(new[] { "one.example.com", "two.example.com" }, new[] { DnsRecordType.A }, options)) {
            answers.Add(answer);
            Assert.Equal(DnsSecValidationStatus.Secure, answer.DnsSecValidationStatus);
            Assert.Equal(Transport.Tcp, answer.UsedTransport);
            Assert.True(answer.DnsSecValidationExpiresUtc <= anchor.Anchor.ValidUntil);
            Assert.InRange(Assert.Single(answer.Answers).TTL, 0, 30);
        }
        Assert.Equal(1, keyQueries);
        Assert.Equal(DnsResponseSource.Network, Assert.Single(answers[0].DnsSecValidationDependencies).ResponseSource);
        Assert.Equal(DnsResponseSource.Cache, Assert.Single(answers[1].DnsSecValidationDependencies).ResponseSource);

        DnsSecZoneTrustAnchor shorter = Anchor(fixture, fixture.Now.AddSeconds(20));
        configuration.DnsSecTrustAnchors = new[] { shorter };
        DnsResponse changedPolicy = await client.Resolve("three.example.com", DnsRecordType.A, options);
        Assert.Equal(DnsSecValidationStatus.Secure, changedPolicy.DnsSecValidationStatus);
        Assert.Equal(2, keyQueries);
        Assert.True(changedPolicy.DnsSecValidationExpiresUtc <= shorter.Anchor.ValidUntil);
        Assert.Equal(DnsResponseSource.Network, Assert.Single(changedPolicy.DnsSecValidationDependencies).ResponseSource);

        configuration.DnsSecTrustAnchors = new[] { Anchor(fixture, fixture.Now.AddSeconds(-1)) };
        DnsResponse expiredPolicy = await client.Resolve("one.example.com", DnsRecordType.A, options);
        Assert.Equal(DnsSecValidationStatus.Bogus, expiredPolicy.DnsSecValidationStatus);
        Assert.False(expiredPolicy.ServedFromCache);
        Assert.Equal(3, keyQueries);
    }

    /// <summary>Early stream disposal drains provider cancellation, and provider failures remain failures without transferring ownership.</summary>
    [Fact]
    public async Task ProviderRemainsCallerOwnedAfterStreamDisposalAndFailure() {
        int fallbackAttempts = 0;
        SystemInformation.SetDnsServerProvider(() => {
            fallbackAttempts++;
            throw new InvalidOperationException("A failed privacy provider must not select System DNS.");
        });
        var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var provider = new TestProvider(async (query, token) => {
            var reader = new DnsWireReader(query, 12);
            if (reader.ReadName().StartsWith("pending.", StringComparison.Ordinal)) {
                try { await Task.Delay(Timeout.Infinite, token); }
                catch (OperationCanceledException) { canceled.TrySetResult(true); throw; }
            }
            return AddressReply(query);
        });
        var request = new ResolveDnsRequest {
            Names = new[] { "ready.example.com", "pending.example.com" },
            Configuration = ProviderConfiguration(provider), RetryCount = 1, MaxConcurrency = 2
        };
        try {
            await using (IAsyncEnumerator<DnsResponse> enumerator = ClientX.QueryDnsStreaming(request).GetAsyncEnumerator()) {
                Assert.True(await enumerator.MoveNextAsync());
                Assert.Equal(Transport.Tcp, enumerator.Current.UsedTransport);
            }
            Assert.Equal(TaskStatus.RanToCompletion, canceled.Task.Status);
            Assert.False(provider.Disposed);
            provider.Exchange = (_, _) => throw new InvalidOperationException("Authenticated provider failure");
            request.Names = new[] { "failed.example.com" };
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(async () => {
                await foreach (DnsResponse _ in ClientX.QueryDnsStreaming(request)) { }
            });
            Assert.Equal("Authenticated provider failure", failure.Message);
            await Assert.ThrowsAsync<InvalidOperationException>(() => ClientX.QueryDns(request));
            Assert.Equal(0, fallbackAttempts);
            Assert.False(provider.Disposed);
        } finally {
            SystemInformation.SetDnsServerProvider(null);
        }
    }

    private static Configuration ProviderConfiguration(TestProvider provider) => new("resolver.example", provider.RequestFormat) {
        WireTransport = provider, TimeOut = 5000
    };

    private static DnsWireTransportResponse AddressReply(byte[] query) {
        var reader = new DnsWireReader(query, 12);
        string name = reader.ReadName();
        return new DnsWireTransportResponse(DnsDiscoveryTestWire.Response(query,
            new DnsDiscoveryTestWire.Record(name, DnsRecordType.A, IPAddress.Parse("192.0.2.9").GetAddressBytes())), Transport.Tcp);
    }

    private static DnsSecZoneTrustAnchor Anchor(DnsSecSignedFixture fixture, DateTimeOffset until) {
        Assert.True(DnsSecCrypto.TryComputeDsDigest(fixture.Zone.Name, fixture.Zone, 2, out byte[] digest));
        return new DnsSecZoneTrustAnchor(fixture.Zone.Name, new DnsSecTrustAnchor(fixture.Zone.KeyTag,
            (DnsKeyAlgorithm)fixture.Zone.Algorithm, 2, BitConverter.ToString(digest).Replace("-", string.Empty), validUntil: until));
    }

    private sealed class TestProvider : IDnsWireTransport, IDisposable {
        internal Func<byte[], CancellationToken, Task<DnsWireTransportResponse>> Exchange;
        internal int QueryCount;
        internal bool Disposed;
        internal TestProvider(Func<byte[], CancellationToken, Task<DnsWireTransportResponse>> exchange) => Exchange = exchange;
        public DnsRequestFormat RequestFormat => DnsRequestFormat.DnsCrypt;
        public Task<DnsWireTransportResponse> QueryAsync(byte[] query, CancellationToken cancellationToken = default) {
            Assert.False(Disposed);
            Interlocked.Increment(ref QueryCount);
            return Exchange(query, cancellationToken);
        }
        public void Dispose() => Disposed = true;
    }
}
