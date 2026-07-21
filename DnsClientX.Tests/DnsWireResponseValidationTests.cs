using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Protects response correlation and bounded wire parsing requirements.
    /// </summary>
    public class DnsWireResponseValidationTests {
        /// <summary>Validates a correctly correlated response.</summary>
        [Fact]
        public async Task ResponseMustMatchTransactionIdAndQuestion() {
            var query = new DnsMessage("example.com", DnsRecordType.A,
                new DnsMessageOptions(TransactionId: 0xBEEF));
            byte[] response = TestUtilities.CreateResponseFromQuery(query.SerializeDnsWireFormat());

            DnsResponse parsed = await DnsWire.DeserializeDnsWireResponse(null, false, response, query);

            Assert.Equal((ushort)0xBEEF, parsed.TransactionId);
            Assert.True(parsed.IsResponse);
            Assert.Single(parsed.Questions);
            Assert.Equal("example.com", parsed.Questions[0].Name);
        }

        /// <summary>Rejects a response with a mismatched transaction identifier.</summary>
        [Fact]
        public async Task ResponseWithWrongTransactionIdIsRejected() {
            var query = new DnsMessage("example.com", DnsRecordType.A,
                new DnsMessageOptions(TransactionId: 0xBEEF));
            byte[] response = TestUtilities.CreateResponseFromQuery(query.SerializeDnsWireFormat());
            response[1] ^= 1;

            DnsClientException error = await Assert.ThrowsAsync<DnsClientException>(
                () => DnsWire.DeserializeDnsWireResponse(null, false, response, query));

            Assert.Contains("transaction ID", error.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Rejects a query packet passed to a response transport.</summary>
        [Fact]
        public async Task QueryPacketIsRejectedAsAResponse() {
            var query = new DnsMessage("example.com", DnsRecordType.A,
                new DnsMessageOptions(TransactionId: 1));

            DnsClientException error = await Assert.ThrowsAsync<DnsClientException>(
                () => DnsWire.DeserializeDnsWireResponse(null, false, query.SerializeDnsWireFormat(), query));

            Assert.Contains("QR=0", error.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Rejects a cyclic or non-backward compression pointer.</summary>
        [Fact]
        public async Task CompressionPointerLoopIsRejected() {
            var query = new DnsMessage("example.com", DnsRecordType.A,
                new DnsMessageOptions(TransactionId: 1));
            byte[] response = {
                0x00, 0x01, 0x81, 0x80, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0xC0, 0x0C, 0x00, 0x01, 0x00, 0x01
            };

            DnsClientException error = await Assert.ThrowsAsync<DnsClientException>(
                () => DnsWire.DeserializeDnsWireResponse(null, false, response, query));

            Assert.Contains("earlier", error.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Parses EDNS extended RCODE and Extended DNS Error options.</summary>
        [Fact]
        public async Task ExtendedRcodeAndEdeAreParsedFromOpt() {
            var query = new DnsMessage("example.com", DnsRecordType.A,
                new DnsMessageOptions(TransactionId: 1));
            var bytes = new List<byte>(TestUtilities.CreateResponseFromQuery(query.SerializeDnsWireFormat()));
            bytes[10] = 0;
            bytes[11] = 1;
            bytes.AddRange(new byte[] {
                0x00, 0x00, 0x29, 0x04, 0xD0,
                0x01, 0x00, 0x00, 0x00,
                0x00, 0x08,
                0x00, 0x0F, 0x00, 0x04, 0x00, 0x06, (byte)'o', (byte)'k'
            });

            DnsResponse parsed = await DnsWire.DeserializeDnsWireResponse(null, false, bytes.ToArray(), query);

            Assert.Equal(DnsResponseCode.BadVersion, parsed.Status);
            Assert.Equal((byte)0, parsed.EdnsVersion);
            Assert.Equal(1232, parsed.EdnsUdpPayloadSize);
            ExtendedDnsError ede = Assert.Single(parsed.ExtendedDnsErrors);
            Assert.Equal(6, ede.InfoCode);
            Assert.Equal("ok", ede.ExtraText);
        }

        /// <summary>OPT is a pseudo-record and is invalid outside the additional section.</summary>
        [Fact]
        public async Task OptRecordInAnswerSectionIsRejected() {
            var query = new DnsMessage("example.com", DnsRecordType.A,
                new DnsMessageOptions(TransactionId: 1));
            var bytes = new List<byte>(TestUtilities.CreateResponseFromQuery(query.SerializeDnsWireFormat()));
            bytes[6] = 0;
            bytes[7] = 1;
            bytes.AddRange(new byte[] {
                0x00, 0x00, 0x29, 0x04, 0xD0,
                0x00, 0x00, 0x00, 0x00,
                0x00, 0x00
            });

            DnsClientException error = await Assert.ThrowsAsync<DnsClientException>(
                () => DnsWire.DeserializeDnsWireResponse(null, false, bytes.ToArray(), query));

            Assert.Contains("additional section", error.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Ensures serialization is stable and CD and DO occupy their RFC-defined fields.</summary>
        [Fact]
        public void MessageUsesStableIdAndPutsCdOnlyInHeader() {
            var message = new DnsMessage("example.com", DnsRecordType.A,
                new DnsMessageOptions(RequestDnsSec: true, CheckingDisabled: true, TransactionId: 0x1234));

            byte[] first = message.SerializeDnsWireFormat();
            byte[] second = message.SerializeDnsWireFormat();

            Assert.Equal(first, second);
            Assert.Equal(0x12, first[0]);
            Assert.Equal(0x34, first[1]);
            Assert.Equal(0x10, first[3] & 0x10);
            int opt = first.Length - 11;
            uint optTtl = ((uint)first[opt + 5] << 24) | ((uint)first[opt + 6] << 16) |
                          ((uint)first[opt + 7] << 8) | first[opt + 8];
            Assert.Equal(0x00008000u, optTtl);
        }

        /// <summary>Preserves large unsigned TTLs without wrapping them negative or pretending they are zero.</summary>
        [Fact]
        public async Task UnsignedTtlAboveInt32IsClampedForTheLegacyIntApi() {
            var query = new DnsMessage("example.com", DnsRecordType.A,
                new DnsMessageOptions(TransactionId: 1));
            var bytes = new List<byte>(TestUtilities.CreateResponseFromQuery(query.SerializeDnsWireFormat()));
            bytes[6] = 0;
            bytes[7] = 1;
            bytes.AddRange(new byte[] {
                0xC0, 0x0C, 0x00, 0x01, 0x00, 0x01,
                0xFF, 0xFF, 0xFF, 0xFF,
                0x00, 0x04, 192, 0, 2, 1
            });

            DnsResponse parsed = await DnsWire.DeserializeDnsWireResponse(null, false, bytes.ToArray(), query);

            Assert.Equal(int.MaxValue, Assert.Single(parsed.Answers).TTL);
        }

        /// <summary>Rejects an HTTP-carried payload that cannot be represented by DNS's 16-bit message length.</summary>
        [Fact]
        public async Task OversizedDnsMessageIsRejected() {
            var query = new DnsMessage("example.com", DnsRecordType.A,
                new DnsMessageOptions(TransactionId: 1));
            var bytes = new byte[ushort.MaxValue + 1];

            DnsClientException error = await Assert.ThrowsAsync<DnsClientException>(
                () => DnsWire.DeserializeDnsWireResponse(null, false, bytes, query));

            Assert.Contains("65535", error.Message, StringComparison.Ordinal);
        }
    }
}
