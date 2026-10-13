using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX;

public partial class ClientX {
    /// <summary>Streams a reusable request with bounded concurrency and shared resolver clients.</summary>
    /// <remarks>Input order is the default. Explicit server fallback proceeds to the next server
    /// only when the completed server produced no successful response, as in QueryDns.
    /// Disposing the stream cancels and drains admitted queries before disposing its clients.</remarks>
    public static async IAsyncEnumerable<DnsResponse> QueryDnsStreaming(ResolveDnsRequest request,
        bool preserveOrder = true, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        if (request == null) throw new ArgumentNullException(nameof(request));
        request.Validate();
        request = request.Snapshot();
        string[] names = request.GetExpandedNames();
        EdnsOptions? edns = request.CreateEdnsOptions();
        DnsResolverEndpoint? selectedEndpoint = null;
        DnsEndpoint? selectedProvider = null;
        if (!string.IsNullOrWhiteSpace(request.ResolverSelectionPath)) {
            ResolverExecutionTarget target = await ResolverExecutionTargetResolver.ResolveSingleAsync(
                new ResolverExecutionTargetSource { ResolverSelectionPath = request.ResolverSelectionPath },
                cancellationToken).ConfigureAwait(false);
            selectedEndpoint = target.ExplicitEndpoint;
            selectedProvider = target.BuiltInEndpoint;
        }
        if (request.Configuration != null) {
            using var client = CreateClientForConfiguration(request, edns);
            await foreach (DnsResponse response in Stream((name, type, token) => QueryOneWithRequestAsync(client, request, name, type, token)).ConfigureAwait(false)) {
                yield return response;
            }
        } else if (request.IsMultiResolverRequest) {
            DnsResolverEndpoint[] endpoints = await ExpandMultiResolverEndpointsAsync(request, cancellationToken).ConfigureAwait(false);
            using var resolver = new DnsMultiResolver(endpoints, CreateMultiResolverOptions(request, edns));
            await foreach (DnsResponse response in Stream(async (name, type, token) => {
                DnsResponse[] results = await ExecuteWithRetryAsync(request,
                    async () => new[] { await resolver.QueryAsync(name, type, token).ConfigureAwait(false) }, token).ConfigureAwait(false);
                return results;
            }).ConfigureAwait(false)) yield return response;
        } else if (request.Servers.Length > 0) {
            List<string> servers = GetValidatedServers(request);
            if (request.RandomServer || request.AllServers && request.Fallback) Shuffle(servers);
            foreach (string server in servers) {
                using var client = CreateClientForServer(request, server, edns);
                bool succeeded = false;
                await foreach (DnsResponse response in Stream((name, type, token) => QueryOneWithRequestAsync(client, request, name, type, token)).ConfigureAwait(false)) {
                    succeeded |= response.Status == DnsResponseCode.NoError;
                    yield return response;
                }
                if (!request.AllServers && (!request.Fallback || succeeded)) break;
            }
        } else {
            using var client = selectedEndpoint != null
                ? CreateClientForEndpoint(request, selectedEndpoint, edns)
                : CreateClientForProvider(request, selectedProvider ?? (request.DnsProviders.Length > 0 ? request.DnsProviders[0] : DnsEndpoint.System), edns);
            await foreach (DnsResponse response in Stream((name, type, token) => QueryOneWithRequestAsync(client, request, name, type, token,
                request.ShouldRequestDnsSec || selectedEndpoint?.DnsSecOk == true)).ConfigureAwait(false)) yield return response;
        }

        async IAsyncEnumerable<DnsResponse> Stream(Func<string, DnsRecordType, CancellationToken, Task<DnsResponse[]>> operation) {
            await foreach (DnsResponse[] batch in DnsConcurrentStream.Execute(EnumerateRequestQueries(names, request.RecordTypes),
                (query, token) => operation(query.Name, query.Type, token), request.EffectiveMaxConcurrency,
                preserveOrder, cancellationToken).ConfigureAwait(false)) {
                foreach (DnsResponse response in batch) yield return response;
            }
        }
    }
}
