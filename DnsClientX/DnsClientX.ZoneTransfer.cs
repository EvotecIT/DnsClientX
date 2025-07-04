using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace DnsClientX {
    public partial class ClientX {
        /// <summary>
        /// Performs a DNS zone transfer (AXFR) using TCP.
        /// </summary>
        /// <param name="zone">Zone name to transfer.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Ordered RRsets as returned by the server.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="DnsClientException">When the transfer fails.</exception>
        public async Task<DnsAnswer[][]> ZoneTransferAsync(string zone, CancellationToken cancellationToken = default) {
            if (string.IsNullOrEmpty(zone)) {
                throw new ArgumentNullException(nameof(zone));
            }

            EndpointConfiguration.SelectHostNameStrategy();

            var query = new DnsMessage(zone, DnsRecordType.AXFR, requestDnsSec: false, enableEdns: false, EndpointConfiguration.UdpBufferSize, null);
            var queryBytes = query.SerializeDnsWireFormat();

            var responses = await SendAxfrOverTcp(queryBytes, EndpointConfiguration.Hostname, EndpointConfiguration.Port, EndpointConfiguration.TimeOut, cancellationToken).ConfigureAwait(false);

            var records = new List<DnsAnswer>();
            int soaCount = 0;
            foreach (var buffer in responses) {
                var res = await DnsWire.DeserializeDnsWireFormat(null, Debug, buffer).ConfigureAwait(false);
                res.AddServerDetails(EndpointConfiguration);
                if (res.Status != DnsResponseCode.NoError) {
                    throw new DnsClientException($"Zone transfer failed with {res.Status}", res);
                }
                if (res.Answers != null) {
                    records.AddRange(res.Answers);
                    soaCount += res.Answers.Count(a => a.Type == DnsRecordType.SOA);
                    if (soaCount >= 2) break;
                }
            }

            var rrsets = new List<List<DnsAnswer>>();
            foreach (var rec in records) {
                if (rrsets.Count == 0 || rrsets[rrsets.Count - 1][0].Name != rec.Name || rrsets[rrsets.Count - 1][0].Type != rec.Type) {
                    rrsets.Add(new List<DnsAnswer> { rec });
                } else {
                    rrsets[rrsets.Count - 1].Add(rec);
                }
            }

            return rrsets.Select(r => r.ToArray()).ToArray();
        }

        /// <summary>
        /// Performs a DNS zone transfer (AXFR) synchronously.
        /// </summary>
        /// <param name="zone">Zone name to transfer.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Ordered RRsets as returned by the server.</returns>
        public DnsAnswer[][] ZoneTransferSync(string zone, CancellationToken cancellationToken = default) {
            return ZoneTransferAsync(zone, cancellationToken).RunSync();
        }

        private static async Task<List<byte[]>> SendAxfrOverTcp(byte[] query, string dnsServer, int port, int timeoutMilliseconds, CancellationToken cancellationToken) {
            using var tcpClient = new TcpClient();
            await ConnectAsync(tcpClient, dnsServer, port, timeoutMilliseconds, cancellationToken).ConfigureAwait(false);
            using var stream = tcpClient.GetStream();

            var lengthBytes = BitConverter.GetBytes((ushort)query.Length);
            if (BitConverter.IsLittleEndian) Array.Reverse(lengthBytes);

            var writeTask = stream.WriteAsync(lengthBytes, 0, lengthBytes.Length, cancellationToken);
            var timeoutTask = Task.Delay(timeoutMilliseconds, cancellationToken);
            if (await Task.WhenAny(writeTask, timeoutTask).ConfigureAwait(false) == timeoutTask) {
                throw new TimeoutException($"Writing length to {dnsServer}:{port} timed out after {timeoutMilliseconds} milliseconds.");
            }
            await writeTask.ConfigureAwait(false);

            writeTask = stream.WriteAsync(query, 0, query.Length, cancellationToken);
            timeoutTask = Task.Delay(timeoutMilliseconds, cancellationToken);
            if (await Task.WhenAny(writeTask, timeoutTask).ConfigureAwait(false) == timeoutTask) {
                throw new TimeoutException($"Writing query to {dnsServer}:{port} timed out after {timeoutMilliseconds} milliseconds.");
            }
            await writeTask.ConfigureAwait(false);

            var responses = new List<byte[]>();
            var lenBuf = new byte[2];
            while (true) {
                try {
                    await ReadExactWithTimeoutAsync(stream, lenBuf, 0, 2, timeoutMilliseconds, cancellationToken).ConfigureAwait(false);
                } catch (EndOfStreamException) {
                    break;
                }
                if (BitConverter.IsLittleEndian) Array.Reverse(lenBuf);
                int length = BitConverter.ToUInt16(lenBuf, 0);
                var responseBuffer = new byte[length];
                await ReadExactWithTimeoutAsync(stream, responseBuffer, 0, length, timeoutMilliseconds, cancellationToken).ConfigureAwait(false);
                responses.Add(responseBuffer);
            }
            return responses;
        }

        private static async Task ReadExactWithTimeoutAsync(NetworkStream stream, byte[] buffer, int offset, int count, int timeoutMilliseconds, CancellationToken cancellationToken) {
            var readTask = DnsWire.ReadExactAsync(stream, buffer, offset, count, cancellationToken);
            var timeoutTask = Task.Delay(timeoutMilliseconds, cancellationToken);
            if (await Task.WhenAny(readTask, timeoutTask) == timeoutTask) {
                throw new TimeoutException($"Reading from stream timed out after {timeoutMilliseconds} milliseconds.");
            }
            await readTask;
        }

        private static async Task ConnectAsync(TcpClient tcpClient, string host, int port, int timeoutMilliseconds, CancellationToken cancellationToken) {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(timeoutMilliseconds);
#if NET5_0_OR_GREATER
            try {
                await tcpClient.ConnectAsync(host, port, linkedCts.Token).ConfigureAwait(false);
            } catch (OperationCanceledException) {
                tcpClient.Close();
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException($"Connection to {host}:{port} timed out after {timeoutMilliseconds} milliseconds.");
            }
#else
            var connectTask = tcpClient.ConnectAsync(host, port);
            var delayTask = Task.Delay(Timeout.Infinite, linkedCts.Token);
            var completed = await Task.WhenAny(connectTask, delayTask).ConfigureAwait(false);
            if (completed != connectTask) {
                tcpClient.Close();
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException($"Connection to {host}:{port} timed out after {timeoutMilliseconds} milliseconds.");
            }
            await connectTask.ConfigureAwait(false);
#endif
        }
    }
}
