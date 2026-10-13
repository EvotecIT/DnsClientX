using System;
using System.Threading;
using System.Threading.Tasks;
using DnsClientX;

namespace DnsClientX.PowerShell.Tests {
    // In-memory DNS wire transport at the public optional-provider boundary. C# 5 compatible for PowerShell 5.1.
    public sealed class ConfigurationWireTransport : IDnsWireTransport, IDisposable {
        public bool Delay;
        public bool Disposed;
        public int Active;
        public int Queries;
        public int Canceled;
        public DnsRequestFormat RequestFormat { get { return DnsRequestFormat.ObliviousDnsOverHttps; } }

        public async Task<DnsWireTransportResponse> QueryAsync(byte[] query, CancellationToken cancellationToken = default(CancellationToken)) {
            if (Disposed) { throw new ObjectDisposedException("ConfigurationWireTransport"); }
            Interlocked.Increment(ref Active);
            Interlocked.Increment(ref Queries);
            try {
                await Task.Delay(Delay ? Timeout.Infinite : 5, cancellationToken).ConfigureAwait(false);
                int end = 12;
                while (query[end] != 0) { end += query[end] + 1; }
                end += 5;
                byte[] answer = new byte[end + 16];
                Array.Copy(query, answer, end);
                answer[2] = 0x81;
                answer[3] = 0x80;
                answer[6] = 0;
                answer[7] = 1;
                answer[8] = answer[9] = answer[10] = answer[11] = 0;
                byte[] record = new byte[] { 0xc0, 0x0c, 0, 1, 0, 1, 0, 0, 0, 60, 0, 4, 127, 0, 0, 5 };
                Array.Copy(record, 0, answer, end, record.Length);
                return new DnsWireTransportResponse(answer, Transport.Doh);
            } catch (OperationCanceledException) {
                Interlocked.Increment(ref Canceled);
                throw;
            } finally {
                Interlocked.Decrement(ref Active);
            }
        }

        public void Dispose() { Disposed = true; }
    }
}
