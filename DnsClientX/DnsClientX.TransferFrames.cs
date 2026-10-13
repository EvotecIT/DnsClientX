using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace DnsClientX {
    public partial class ClientX {
        private readonly struct TransferFrame {
            internal TransferFrame(byte[] data, bool signed) { Data = data; Signed = signed; }
            internal byte[] Data { get; }
            internal bool Signed { get; }
        }

        // Legacy servers may sign only every hundredth frame. Buffer their bounded unsigned
        // window so the streaming API never exposes records before a MAC authenticates them.
        private static async IAsyncEnumerable<TransferFrame> ReadTransferFramesAsync(Stream stream,
            Configuration configuration, ushort transactionId, byte[]? requestMac,
            [EnumeratorCancellation] CancellationToken cancellationToken) {
            using var verifier = configuration.TsigKey == null ? null
                : new DnsTsig.TransferVerifier(configuration.TsigKey, transactionId, requestMac!);
            var pending = new List<TransferFrame>();
            var lengthBuffer = new byte[2];
            int messageCount = 0;
            long transferredBytes = 0;
            while (true) {
                try {
                    await ReadExactWithTimeoutAsync(stream, lengthBuffer, 0, 2, configuration.TimeOut,
                        cancellationToken).ConfigureAwait(false);
                } catch (EndOfStreamException) {
                    if (messageCount == 0) throw new DnsClientException("The server closed the transfer stream before returning a response.", new EndOfStreamException());
                    verifier?.RequireComplete();
                    yield break;
                }
                int length = (lengthBuffer[0] << 8) | lengthBuffer[1];
                if (++messageCount > configuration.MaxZoneTransferMessages) {
                    throw new DnsClientException($"Zone transfer exceeded the configured {configuration.MaxZoneTransferMessages} message limit.");
                }
                transferredBytes += length + 2L;
                if (transferredBytes > configuration.MaxZoneTransferBytes) {
                    throw new DnsClientException($"Zone transfer exceeded the configured {configuration.MaxZoneTransferBytes} byte limit.");
                }
                var frame = new byte[length];
                await ReadExactWithTimeoutAsync(stream, frame, 0, length, configuration.TimeOut,
                    cancellationToken).ConfigureAwait(false);
                ValidateTransferEnvelope(frame, transactionId);
                if (verifier == null) {
                    yield return new TransferFrame(frame, false);
                    continue;
                }
                bool signed = verifier.Authenticate(frame);
                pending.Add(new TransferFrame(frame, signed));
                if (!signed) continue;
                foreach (TransferFrame authenticated in pending) yield return authenticated;
                pending.Clear();
            }
        }

        private static void RequireSignedTransferEnd(Configuration configuration, TransferFrame frame) {
            if (configuration.TsigKey != null && !frame.Signed) {
                throw new DnsClientException("The closing transfer message must contain TSIG.");
            }
        }
    }
}
