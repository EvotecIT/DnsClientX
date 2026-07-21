using System;
using System.Collections.Generic;
using System.IO;
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

        private static void UInt32(Stream output, uint value) {
            output.WriteByte((byte)(value >> 24));
            output.WriteByte((byte)(value >> 16));
            output.WriteByte((byte)(value >> 8));
            output.WriteByte((byte)value);
        }
    }
}
