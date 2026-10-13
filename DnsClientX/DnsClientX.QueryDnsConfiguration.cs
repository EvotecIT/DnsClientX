using System;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX;

public partial class ClientX {
    /// <summary>Executes a supplied configuration without translating its transport or security state through an endpoint adapter.</summary>
    private static async Task<DnsResponse[]> ExecuteConfigurationAsync(ResolveDnsRequest request, string[] names,
        EdnsOptions? ednsOptions, CancellationToken cancellationToken) {
        // Freeze caller-owned transport/security values before retry or network awaits.
        Configuration snapshot = request.Configuration!.CreateQuerySnapshot();
        var options = new ResolverExecutionClientOptions {
            TimeoutMs = request.ConfigurationTimeoutMilliseconds,
            BootstrapResolver = request.BootstrapResolver,
            DnsSecSignatureVerifier = request.DnsSecSignatureVerifier,
            DnsSecTrustAnchors = request.DnsSecTrustAnchors
        };
        if (request.MaxConcurrency > 0) {
            snapshot.MaxConcurrency = request.MaxConcurrency;
        }
        if (request.CheckingDisabled) {
            snapshot.CheckingDisabled = true;
        }
        if (ednsOptions != null) {
            snapshot.EdnsOptions = ednsOptions;
        }
        return await ExecuteWithRetryAsync(request, async () => {
            // A fresh client owns its connections/cache only. The optional transport remains caller-owned.
            using var client = ResolverExecutionClientFactory.CreateConfiguredClient(snapshot, options);
            return await QueryWithRequestAsync(client, request, names, cancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }
}
