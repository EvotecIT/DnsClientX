using System;
using System.Threading;
using System.Threading.Tasks;
using DnsClientX;

namespace DnsClientX.PowerShell.Tests {
    // Provider-loading and caller-ownership fixture, not a privacy cryptography implementation.
    public sealed class PrivacyTransportFactory : IDnsWireTransportFactory {
        public IDnsWireTransportProvider Create(string resolver, string relay = null) {
            return new PrivacyTransport(resolver, relay);
        }
    }
    public sealed class PrivacyTransport : IDnsWireTransportProvider {
        public string Resolver { get; private set; }
        public string Relay { get; private set; }
        public bool Disposed { get; private set; }
        public DnsRequestFormat RequestFormat { get { return DnsRequestFormat.ObliviousDnsOverHttps; } }
        public PrivacyTransport(string resolver, string relay) { Resolver = resolver; Relay = relay; }
        public Configuration CreateConfiguration() {
            var configuration = new Configuration(new Uri("https://fixture.invalid/dns-query"), RequestFormat);
            configuration.WireTransport = this;
            return configuration;
        }
        public Task<DnsWireTransportResponse> QueryAsync(byte[] query, CancellationToken cancellationToken = default(CancellationToken)) {
            cancellationToken.ThrowIfCancellationRequested();
            if (Disposed) { throw new ObjectDisposedException("PrivacyTransport"); }
            byte[] response = (byte[])query.Clone(); response[2] = 0x81; response[3] = 0x80;
            return Task.FromResult(new DnsWireTransportResponse(response, Transport.Doh));
        }
        public void Dispose() { Disposed = true; }
    }
}
