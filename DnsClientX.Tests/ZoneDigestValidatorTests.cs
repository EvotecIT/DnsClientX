using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>Tests RFC 8976 zone-digest validation against preserved wire records.</summary>
    public class ZoneDigestValidatorTests {
        private const string Zone = "example.com.";

        /// <summary>SHA-384 validates and is explicitly not presented as origin authentication.</summary>
        [Fact]
        public void Validate_AcceptsSha384Digest() {
            ZoneCanonicalRecord[] records = BaseRecords();
            byte[] digest = ZoneDigestValidator.ComputeDigest(Zone, records, 1);
            records = WithZoneMd(records, ZoneMd(2026072101, 1, 1, digest));

            ZoneDigestValidationResult result = ZoneDigestValidator.Validate(Zone, Transfer(records));

            Assert.True(result.IsValid);
            Assert.Equal(ZoneDigestValidationStatus.Valid, result.Status);
            Assert.Equal((uint)2026072101, result.Serial);
            Assert.Equal((byte)1, result.Scheme);
            Assert.Equal((byte)1, result.HashAlgorithm);
            Assert.False(result.ProvidesOriginAuthentication);
            Assert.Contains("does not", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>SHA-512 is a second supported RFC 8976 hash algorithm.</summary>
        [Fact]
        public void Validate_AcceptsSha512Digest() {
            ZoneCanonicalRecord[] records = BaseRecords();
            byte[] digest = ZoneDigestValidator.ComputeDigest(Zone, records, 2);
            records = WithZoneMd(records, ZoneMd(2026072101, 1, 2, digest));

            ZoneDigestValidationResult result = ZoneDigestValidator.Validate(Zone, Transfer(records));

            Assert.True(result.IsValid);
            Assert.Equal((byte)2, result.HashAlgorithm);
        }

        /// <summary>A changed zone record cannot validate against a prior digest.</summary>
        [Fact]
        public void Validate_RejectsChangedZoneData() {
            ZoneCanonicalRecord[] original = BaseRecords();
            byte[] digest = ZoneDigestValidator.ComputeDigest(Zone, original, 1);
            ZoneCanonicalRecord[] changed = BaseRecords(new byte[] { 192, 0, 2, 99 });
            changed = WithZoneMd(changed, ZoneMd(2026072101, 1, 1, digest));

            ZoneDigestValidationResult result = ZoneDigestValidator.Validate(Zone, Transfer(changed));

            Assert.Equal(ZoneDigestValidationStatus.Invalid, result.Status);
        }

        /// <summary>The ZONEMD serial is bound to the apex SOA serial.</summary>
        [Fact]
        public void Validate_RejectsSerialMismatch() {
            ZoneCanonicalRecord[] records = BaseRecords();
            byte[] digest = ZoneDigestValidator.ComputeDigest(Zone, records, 1);
            records = WithZoneMd(records, ZoneMd(2026072100, 1, 1, digest));

            ZoneDigestValidationResult result = ZoneDigestValidator.Validate(Zone, Transfer(records));

            Assert.Equal(ZoneDigestValidationStatus.Invalid, result.Status);
            Assert.Contains("serial", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Duplicate scheme/hash tuples are unusable under RFC 8976.</summary>
        [Fact]
        public void Validate_RejectsDuplicateSchemeAndHashTuple() {
            ZoneCanonicalRecord[] records = BaseRecords();
            byte[] digest = ZoneDigestValidator.ComputeDigest(Zone, records, 1);
            ZoneCanonicalRecord zoneMd = ZoneMd(2026072101, 1, 1, digest);
            records = WithZoneMd(records, zoneMd, zoneMd);

            ZoneDigestValidationResult result = ZoneDigestValidator.Validate(Zone, Transfer(records));

            Assert.Equal(ZoneDigestValidationStatus.Invalid, result.Status);
            Assert.Contains("unique", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Absence and unsupported algorithms remain distinguishable from a bad digest.</summary>
        [Fact]
        public void Validate_ReportsMissingAndUnsupported() {
            ZoneCanonicalRecord[] records = BaseRecords();
            Assert.Equal(ZoneDigestValidationStatus.Missing,
                ZoneDigestValidator.Validate(Zone, Transfer(records)).Status);

            records = WithZoneMd(records, ZoneMd(2026072101, 2, 200, new byte[48]));
            Assert.Equal(ZoneDigestValidationStatus.Unsupported,
                ZoneDigestValidator.Validate(Zone, Transfer(records)).Status);
        }

        /// <summary>Presentation-only results cannot be silently treated as canonical zone data.</summary>
        [Fact]
        public void Validate_RejectsNonWirePreservingInput() {
            var result = new ZoneTransferResult(
                new[] { new DnsAnswer { Name = Zone, Type = DnsRecordType.SOA, DataRaw = "presentation only" } },
                true, false, 0);

            ZoneDigestValidationResult validation = ZoneDigestValidator.Validate(Zone, new[] { result });

            Assert.Equal(ZoneDigestValidationStatus.Invalid, validation.Status);
            Assert.Contains("wire-preserving", validation.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>A valid prefix cannot authenticate a transfer whose closing SOA was never received.</summary>
        [Fact]
        public void Validate_RejectsTransferWithoutClosingBoundary() {
            ZoneCanonicalRecord[] records = BaseRecords();
            byte[] digest = ZoneDigestValidator.ComputeDigest(Zone, records, 1);
            records = WithZoneMd(records, ZoneMd(2026072101, 1, 1, digest));
            ZoneTransferResult complete = Transfer(records)[0];
            var incomplete = new ZoneTransferResult(complete.Records, complete.CanonicalRecords,
                isOpening: true, isClosing: false, index: 0);

            ZoneDigestValidationResult result = ZoneDigestValidator.Validate(Zone, new[] { incomplete });

            Assert.Equal(ZoneDigestValidationStatus.Invalid, result.Status);
            Assert.Contains("complete AXFR", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Chunk gaps cannot be hidden by collecting only selected pieces of a transfer.</summary>
        [Fact]
        public void Validate_RejectsNonContiguousTransferChunks() {
            ZoneCanonicalRecord[] records = BaseRecords();
            var opening = new ZoneTransferResult(
                new[] { new DnsAnswer { Name = Zone, Type = DnsRecordType.SOA } },
                new[] { records[0] }, true, false, 0);
            var closing = new ZoneTransferResult(
                new[] { new DnsAnswer { Name = Zone, Type = DnsRecordType.SOA } },
                new[] { records[records.Length - 1] }, false, true, 2);

            ZoneDigestValidationResult result = ZoneDigestValidator.Validate(Zone, new[] { opening, closing });

            Assert.Equal(ZoneDigestValidationStatus.Invalid, result.Status);
            Assert.Contains("complete AXFR", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The RFC 8976 Appendix A.1 published test vector prevents self-consistent implementation errors.</summary>
        [Fact]
        public void ComputeDigest_MatchesRfc8976SimpleExample() {
            const uint serial = 2018031900;
            ZoneCanonicalRecord[] records = {
                Record("example.", DnsRecordType.SOA, Soa(serial, "ns1.example.", "admin.example.",
                    1800, 900, 604800, 86400), 86400),
                Record("example.", DnsRecordType.NS, Name("ns1.example."), 86400),
                Record("example.", DnsRecordType.NS, Name("ns2.example."), 86400),
                ZoneMd(serial, 1, 1, new byte[48], "example.", 86400),
                Record("ns1.example.", DnsRecordType.A, new byte[] { 203, 0, 113, 63 }),
                Record("ns2.example.", DnsRecordType.AAAA,
                    new byte[] { 0x20, 0x01, 0x0d, 0xb8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x63 }),
                // An AXFR must not contain this, but RFC 8976's complex vector confirms out-of-zone data is excluded.
                Record("outside.test.", DnsRecordType.A, new byte[] { 192, 0, 2, 1 })
            };

            byte[] actual = ZoneDigestValidator.ComputeDigest("example.", records, 1);

            Assert.Equal(
                "c68090d90a7aed716bc459f9340e3d7c1370d4d24b7e2fc3a1ddc0b9a87153b9a9713b3c9ae5cc27777f98b8e730044c",
                BitConverter.ToString(actual).Replace("-", string.Empty).ToLowerInvariant());
        }

        /// <summary>Post-RFC 3597 embedded names retain case in DNSSEC and ZONEMD canonical form.</summary>
        [Theory]
        [InlineData(DnsRecordType.IPSECKEY)]
        [InlineData(DnsRecordType.HIP)]
        [InlineData(DnsRecordType.TALINK)]
        [InlineData(DnsRecordType.LP)]
        [InlineData(DnsRecordType.SVCB)]
        [InlineData(DnsRecordType.HTTPS)]
        [InlineData(DnsRecordType.TKEY)]
        [InlineData(DnsRecordType.TSIG)]
        [InlineData(DnsRecordType.AMTRELAY)]
        public void CanonicalRecord_PreservesCaseInModernUncompressedName(DnsRecordType type) {
            byte[] target = NamePreservingCase("MiXeD.Example.com.");
            byte[] rdata = ModernNameRdata(type, target);

            ZoneCanonicalRecord record = Record(Zone, type, rdata);

            Assert.Equal(rdata, record.CanonicalRdata);
        }

        /// <summary>Known modern name-bearing RDATA rejects prohibited pointers instead of hashing offsets.</summary>
        [Theory]
        [InlineData(DnsRecordType.IPSECKEY)]
        [InlineData(DnsRecordType.HIP)]
        [InlineData(DnsRecordType.TALINK)]
        [InlineData(DnsRecordType.LP)]
        [InlineData(DnsRecordType.SVCB)]
        [InlineData(DnsRecordType.HTTPS)]
        [InlineData(DnsRecordType.TKEY)]
        [InlineData(DnsRecordType.TSIG)]
        [InlineData(DnsRecordType.AMTRELAY)]
        public void CanonicalRecord_RejectsCompressedModernNameRdata(DnsRecordType type) {
            byte[] prefix = Name("target.example.com.");
            byte[] rdata = CompressedModernRdata(type);
            var message = new byte[prefix.Length + rdata.Length];
            Buffer.BlockCopy(prefix, 0, message, 0, prefix.Length);
            Buffer.BlockCopy(rdata, 0, message, prefix.Length, rdata.Length);
            var wire = new DnsWireResourceRecord(Zone, type, 1, 3600, 3600, prefix.Length,
                checked((ushort)rdata.Length), string.Empty);

            DnsClientException exception = Assert.Throws<DnsClientException>(() =>
                ZoneCanonicalRecord.Create(message, wire));

            Assert.Contains("must not use DNS compression", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>Legacy compression is expanded so pointer offsets never affect the digest.</summary>
        [Fact]
        public void CanonicalRecord_ExpandsLegacyCompressedNameIndependentlyOfPointerOffset() {
            ZoneCanonicalRecord first = CompressedNameRecord(DnsRecordType.NS, "Ns1.Example.com.", 0);
            ZoneCanonicalRecord second = CompressedNameRecord(DnsRecordType.NS, "Ns1.Example.com.", 19);

            Assert.Equal(Name("ns1.example.com."), first.CanonicalRdata);
            Assert.Equal(first.CanonicalRdata, second.CanonicalRdata);
            Assert.Equal(first.CanonicalWire, second.CanonicalWire);
        }

        private static ZoneCanonicalRecord[] BaseRecords(byte[]? address = null) {
            ZoneCanonicalRecord soa = Record(Zone, DnsRecordType.SOA, Soa(2026072101));
            return new[] {
                soa,
                Record("www.example.com.", DnsRecordType.A, address ?? new byte[] { 192, 0, 2, 1 }),
                Record(Zone, DnsRecordType.NS, Name("ns1.example.com.")),
                Record("ns1.example.com.", DnsRecordType.A, new byte[] { 192, 0, 2, 53 }),
                soa
            };
        }

        private static ZoneCanonicalRecord[] WithZoneMd(ZoneCanonicalRecord[] records,
            params ZoneCanonicalRecord[] zoneMdRecords) {
            var output = new List<ZoneCanonicalRecord>(records.Length + zoneMdRecords.Length);
            output.AddRange(records);
            output.InsertRange(records.Length - 1, zoneMdRecords);
            return output.ToArray();
        }

        private static ZoneTransferResult[] Transfer(ZoneCanonicalRecord[] records) {
            var answers = new DnsAnswer[records.Length];
            for (int i = 0; i < records.Length; i++) {
                answers[i] = new DnsAnswer { Name = records[i].Name, Type = records[i].Type, TTL = 3600 };
            }
            return new[] { new ZoneTransferResult(answers, records, true, true, 0) };
        }

        private static ZoneCanonicalRecord ZoneMd(uint serial, byte scheme, byte hashAlgorithm, byte[] digest,
            string owner = Zone, uint ttl = 3600) {
            using var output = new MemoryStream();
            UInt32(output, serial);
            output.WriteByte(scheme);
            output.WriteByte(hashAlgorithm);
            output.Write(digest, 0, digest.Length);
            return Record(owner, DnsRecordType.ZONEMD, output.ToArray(), ttl);
        }

        private static ZoneCanonicalRecord Record(string owner, DnsRecordType type, byte[] rdata,
            uint ttl = 3600) {
            var wire = new DnsWireResourceRecord(owner, type, 1, checked((int)ttl), ttl, 0,
                checked((ushort)rdata.Length), string.Empty);
            return ZoneCanonicalRecord.Create(rdata, wire);
        }

        private static byte[] Soa(uint serial, string primary = "ns1.example.com.",
            string mailbox = "hostmaster.example.com.", uint refresh = 3600, uint retry = 600,
            uint expire = 86400, uint minimum = 60) {
            using var output = new MemoryStream();
            byte[] primaryWire = Name(primary);
            byte[] mailboxWire = Name(mailbox);
            output.Write(primaryWire, 0, primaryWire.Length);
            output.Write(mailboxWire, 0, mailboxWire.Length);
            UInt32(output, serial);
            UInt32(output, refresh);
            UInt32(output, retry);
            UInt32(output, expire);
            UInt32(output, minimum);
            return output.ToArray();
        }

        private static byte[] Name(string name) => DnsWireNameCodec.ToCanonicalWire(name);

        private static byte[] NamePreservingCase(string name) {
            using var output = new MemoryStream();
            foreach (string label in name.TrimEnd('.').Split('.')) {
                byte[] value = Encoding.ASCII.GetBytes(label);
                output.WriteByte(checked((byte)value.Length));
                output.Write(value, 0, value.Length);
            }
            output.WriteByte(0);
            return output.ToArray();
        }

        private static byte[] CompressedModernRdata(DnsRecordType type) {
            byte[] pointer = { 0xC0, 0x00 };
            return ModernNameRdata(type, pointer);
        }

        private static byte[] ModernNameRdata(DnsRecordType type, byte[] name) {
            switch (type) {
                case DnsRecordType.IPSECKEY:
                    return Combine(new byte[] { 10, 3, 2 }, name);
                case DnsRecordType.HIP:
                    return Combine(new byte[] { 0, 0, 0, 0 }, name);
                case DnsRecordType.TALINK:
                    return Combine(name, new byte[] { 0 });
                case DnsRecordType.LP:
                case DnsRecordType.SVCB:
                case DnsRecordType.HTTPS:
                    return Combine(new byte[] { 0, 1 }, name);
                case DnsRecordType.AMTRELAY:
                    return Combine(new byte[] { 10, 3 }, name);
                case DnsRecordType.TKEY:
                case DnsRecordType.TSIG:
                    return name;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        private static byte[] Combine(params byte[][] values) {
            using var output = new MemoryStream();
            foreach (byte[] value in values) output.Write(value, 0, value.Length);
            return output.ToArray();
        }

        private static ZoneCanonicalRecord CompressedNameRecord(DnsRecordType type, string target,
            int targetOffset) {
            byte[] targetWire = NamePreservingCase(target);
            int rdataOffset = targetOffset + targetWire.Length;
            var message = new byte[rdataOffset + 2];
            Buffer.BlockCopy(targetWire, 0, message, targetOffset, targetWire.Length);
            message[rdataOffset] = (byte)(0xC0 | (targetOffset >> 8));
            message[rdataOffset + 1] = (byte)targetOffset;
            var wire = new DnsWireResourceRecord(Zone, type, 1, 3600, 3600, rdataOffset, 2, string.Empty);
            return ZoneCanonicalRecord.Create(message, wire);
        }

        private static void UInt32(Stream output, uint value) {
            output.WriteByte((byte)(value >> 24));
            output.WriteByte((byte)(value >> 16));
            output.WriteByte((byte)(value >> 8));
            output.WriteByte((byte)value);
        }
    }
}
