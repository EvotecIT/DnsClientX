using System;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX;

public partial class ClientX {
    private async Task<DnsResponse> ResolveOptionalWireTransportAsync(Configuration configuration,
        string name, DnsRecordType type, bool requestDnsSec, bool wireValidationQuery,
        CancellationToken cancellationToken) {
        IDnsWireTransport provider = configuration.WireTransport!;
        if (provider.RequestFormat != configuration.RequestFormat) {
            throw new ArgumentException("The optional wire transport does not match the configured request format.");
        }
        if (provider.RequestFormat != DnsRequestFormat.ObliviousDnsOverHttps
            && provider.RequestFormat != DnsRequestFormat.DnsCrypt
            && provider.RequestFormat != DnsRequestFormat.DnsCryptRelay) {
            throw new ArgumentException("Optional wire transports must implement ODoH or DNSCrypt.");
        }
        DnsMessage query = DnsWireQueryBuilder.BuildQuery(name, type, requestDnsSec, configuration,
            checkingDisabled: configuration.CheckingDisabled || wireValidationQuery);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(configuration.TimeOut);
        DnsWireTransportResponse exchange = await provider.QueryAsync(query.SerializeDnsWireFormat(), timeout.Token).ConfigureAwait(false);
        timeout.Token.ThrowIfCancellationRequested();
        DnsResponse response = await DnsWire.DeserializeDnsWireResponse(null, Debug, exchange.Message, query).ConfigureAwait(false);
        response.AddServerDetails(configuration);
        response.UsedTransport = exchange.Transport;
        return response;
    }
}
