using System;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Contract tests for RFC 2136 update encoding and RFC 8945 TSIG authentication.
    /// </summary>
    [Collection("NoParallel")]
    public class DnsTsigTests {
        /// <summary>Verifies that a signed update and its response share the RFC 8945 MAC chain.</summary>
        [Fact]
        public void SignedUpdateAndAuthenticatedResponseRoundTrip() {
            Func<DateTimeOffset> originalClock = DnsTsig.UtcNow;
            try {
                DnsTsig.UtcNow = () => DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
                var key = new TsigKey("update-key.example.com", new byte[] { 1, 3, 3, 7, 9, 11 });
                DnsUpdateRequestMessage request = DnsUpdateMessage.CreateAddMessage(
                    "example.com", "www.example.com", DnsRecordType.A, "192.0.2.10", 300, key);

                Assert.Equal(1, ReadUInt16(request.WireData, 10));
                Assert.NotEmpty(request.TsigMac);

                byte[] unsignedResponse = TestUtilities.CreateResponseFromQuery(request.WireData, 0xA800);
                DnsTsigSignedMessage signedResponse = DnsTsig.SignResponse(
                    unsignedResponse, request.TransactionId, key, request.TsigMac);

                DnsTsig.VerifyResponse(signedResponse.WireData, request.TransactionId, key, request.TsigMac);

                signedResponse.WireData[3] ^= 1;
                Assert.Throws<DnsClientException>(() =>
                    DnsTsig.VerifyResponse(signedResponse.WireData, request.TransactionId, key, request.TsigMac));
            } finally {
                DnsTsig.UtcNow = originalClock;
            }
        }

        /// <summary>Verifies RFC 2136 class and RDATA rules for RRset and value deletion.</summary>
        [Fact]
        public void DeleteRrsetAndDeleteValueUseDifferentRfc2136Classes() {
            DnsUpdateRequestMessage rrset = DnsUpdateMessage.CreateDeleteRrsetMessage(
                "example.com", "www.example.com", DnsRecordType.A);
            DnsUpdateRequestMessage value = DnsUpdateMessage.CreateDeleteValueMessage(
                "example.com", "www.example.com", DnsRecordType.A, "192.0.2.10");

            int rrsetRecord = UpdateRecordOffset(rrset.WireData);
            int valueRecord = UpdateRecordOffset(value.WireData);
            Assert.Equal(255, ReadUInt16(rrset.WireData, rrsetRecord + 2));
            Assert.Equal(0u, ReadUInt32(rrset.WireData, rrsetRecord + 4));
            Assert.Equal(0, ReadUInt16(rrset.WireData, rrsetRecord + 8));
            Assert.Equal(254, ReadUInt16(value.WireData, valueRecord + 2));
            Assert.Equal(0u, ReadUInt32(value.WireData, valueRecord + 4));
            Assert.Equal(4, ReadUInt16(value.WireData, valueRecord + 8));
        }

        /// <summary>Ensures unsupported RDATA types are rejected instead of encoded as misleading ASCII.</summary>
        [Fact]
        public void UnsupportedUpdateRdataFailsInsteadOfSendingAsciiBytes() {
            Assert.Throws<NotSupportedException>(() => DnsUpdateMessage.CreateAddMessage(
                "example.com", "example.com", DnsRecordType.SOA, "not wire data", 300));
        }

        private static int UpdateRecordOffset(byte[] message) {
            int offset = SkipName(message, 12) + 4;
            return SkipName(message, offset);
        }

        private static int SkipName(byte[] value, int offset) {
            while (value[offset] != 0) offset += value[offset] + 1;
            return offset + 1;
        }

        private static ushort ReadUInt16(byte[] value, int offset) => (ushort)((value[offset] << 8) | value[offset + 1]);
        private static uint ReadUInt32(byte[] value, int offset) =>
            ((uint)value[offset] << 24) | ((uint)value[offset + 1] << 16) | ((uint)value[offset + 2] << 8) | value[offset + 3];
    }
}
