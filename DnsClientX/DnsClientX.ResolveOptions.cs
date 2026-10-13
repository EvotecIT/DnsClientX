using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX;

public partial class ClientX {
    /// <summary>Resolves one name using explicit query behavior and this client's resolver.</summary>
    public Task<DnsResponse> Resolve(string name, DnsRecordType type, DnsResolveOptions options,
        CancellationToken cancellationToken = default) {
        if (options == null) throw new ArgumentNullException(nameof(options));
        DnsResolveOptions settings = options.Snapshot();
        return ResolveWithOptions(name, type, settings, cancellationToken);
    }

    private Task<DnsResponse> ResolveWithOptions(string name, DnsRecordType type, DnsResolveOptions options,
        CancellationToken cancellationToken) => Resolve(name, type, options.RequestDnsSec, options.ValidateDnsSec,
            options.ReturnAllTypes, options.RetryOnTransient, options.MaxRetries, options.RetryDelayMs,
            options.TypedRecords, options.ParseTypedTxtRecords, cancellationToken);

    /// <summary>Resolves a bounded batch in name and record type input order.</summary>
    public async Task<DnsResponse[]> Resolve(string[] names, DnsRecordType[] types, DnsResolveOptions options,
        CancellationToken cancellationToken = default) {
        var results = new List<DnsResponse>();
        await foreach (DnsResponse response in ResolveStreaming(names, types, options, true, cancellationToken).ConfigureAwait(false)) {
            results.Add(response);
        }
        return results.ToArray();
    }

    /// <summary>Streams names and types using the configured concurrency limit, or 32 when unset.</summary>
    /// <remarks>Input order is the default. Early disposal cancels and drains admitted work.</remarks>
    public IAsyncEnumerable<DnsResponse> ResolveStreaming(IEnumerable<string> names, IEnumerable<DnsRecordType> types,
        DnsResolveOptions options, bool preserveOrder = true, CancellationToken cancellationToken = default) {
        if (names == null) throw new ArgumentNullException(nameof(names));
        if (types == null) throw new ArgumentNullException(nameof(types));
        if (options == null) throw new ArgumentNullException(nameof(options));
        ThrowIfDisposed();
        DnsResolveOptions settings = options.Snapshot();
        // Record types are a small reusable query specification; names remain lazy.
        var recordTypes = new List<DnsRecordType>(types).ToArray();
        return DnsConcurrentStream.Execute(QueryPairs(names, recordTypes),
            (query, token) => ResolveWithOptions(query.Name, query.Type, settings, token),
            EndpointConfiguration.MaxConcurrency, preserveOrder, cancellationToken);
    }
}
