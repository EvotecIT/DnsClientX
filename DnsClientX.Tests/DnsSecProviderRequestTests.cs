using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DnsClientX.DnsSec.EdDsa;

namespace DnsClientX.Tests;

/// <summary>Protects provider selection through direct and saved iterative-root requests.</summary>
[Collection("NoParallel")]
public class DnsSecProviderRequestTests {
    /// <summary>Root requests use the shared configured-client pipeline for both selection forms.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RootRequestsPreserveProvider(bool savedSelection) {
        string path = Path.Combine(Path.GetTempPath(), "DnsClientX-root-provider-" + Guid.NewGuid().ToString("N") + ".json");
        var provider = new EdDsaDnsSecSignatureVerifier();
        var request = new ResolveDnsRequest {
            Names = new[] { "example.com" }, RecordTypes = new[] { DnsRecordType.SOA },
            DnsProviders = savedSelection ? Array.Empty<DnsEndpoint>() : new[] { DnsEndpoint.RootServer },
            DnsSecSignatureVerifier = provider, ValidateDnsSec = true,
            TimeOutMilliseconds = 1000, RetryCount = 1
        };
        if (savedSelection) {
            ResolverScoreStore.Save(path, new ResolverScoreSnapshot {
                Summary = new ResolverScoreSummary { Mode = ResolverScoreMode.Probe,
                    RecommendationAvailable = true, RecommendedTarget = "RootServer",
                    RecommendedResolver = "RootServer", RecommendedTransport = "Udp", RecommendedAverageMs = 5 }
            });
            request.ResolverSelectionPath = path;
        }
        bool reachedSharedPipeline = false;
        try {
            ClientX.QueryDnsRequestOverride = (observed, names, type, token) => {
                reachedSharedPipeline = true;
                Assert.Same(provider, observed.DnsSecSignatureVerifier);
                Assert.True(observed.ShouldValidateDnsSec);
                return Task.FromResult(new[] { new DnsResponse { Status = DnsResponseCode.NoError } });
            };
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            Assert.Single(await ClientX.QueryDns(request, timeout.Token));
            Assert.True(reachedSharedPipeline);
            using var configured = (ClientX)typeof(ClientX).GetMethod("CreateClientForProvider", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, new object?[] { request, DnsEndpoint.RootServer, null })!;
            Assert.Same(provider, configured.EndpointConfiguration.DnsSecSignatureVerifier);
        } finally {
            ClientX.QueryDnsRequestOverride = null;
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
