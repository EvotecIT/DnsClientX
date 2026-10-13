using System;
using System.IO;

namespace DnsClientX {
    internal static partial class DnsTsig {
        /// <summary>Authenticates one RFC 8945 TCP response sequence.</summary>
        internal sealed class TransferVerifier : IDisposable {
            private readonly TsigKey key;
            private readonly ushort transactionId;
            private byte[] priorMac;
            private readonly MemoryStream pending = new MemoryStream();
            private bool started;
            private ulong lastTime;
            private int unsignedCount;

            internal TransferVerifier(TsigKey key, ushort transactionId, byte[] requestMac) {
                this.key = key;
                this.transactionId = transactionId;
                priorMac = requestMac;
            }

            /// <summary>Returns true only when the current frame authenticates the pending sequence.</summary>
            internal bool Authenticate(byte[] frame) {
                ParsedTsig? parsed = ParseResponse(frame, transactionId, key);
                if (parsed == null) {
                    if (!started) throw new DnsClientException("The first authenticated transfer message is missing TSIG.");
                    if (++unsignedCount > 99) throw new DnsClientException("A TSIG transfer cannot contain more than 99 consecutive unsigned intermediary messages.");
                    pending.Write(frame, 0, frame.Length);
                    return false;
                }
                if (!started) {
                    VerifyResponse(frame, transactionId, key, priorMac);
                } else {
                    if (parsed.Time < lastTime) throw new DnsClientException("TSIG transfer signing times cannot move backwards.");
                    using var input = new MemoryStream();
                    WriteUInt16(input, checked((ushort)priorMac.Length));
                    input.Write(priorMac, 0, priorMac.Length);
                    pending.Position = 0;
                    pending.CopyTo(input);
                    input.Write(parsed.UnsignedMessage, 0, parsed.UnsignedMessage.Length);
                    WriteUInt48(input, parsed.Time);
                    WriteUInt16(input, parsed.Fudge);
                    ValidateMac(ComputeMac(key, input.ToArray()), parsed, priorMac.Length);
                }
                started = true;
                lastTime = parsed.Time;
                priorMac = parsed.Mac;
                unsignedCount = 0;
                pending.SetLength(0);
                return true;
            }

            internal void RequireComplete() {
                if (!started || unsignedCount != 0) throw new DnsClientException("The final authenticated transfer message is missing TSIG.");
            }

            public void Dispose() => pending.Dispose();
        }

    }
}
