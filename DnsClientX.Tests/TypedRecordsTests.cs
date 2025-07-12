using System.Net;
using DnsClientX;
using Xunit;

namespace DnsClientX.Tests {
    public class TypedRecordsTests {
        [Fact]
        public void Factory_Parses_A_Record() {
            var ans = new DnsAnswer { Type = DnsRecordType.A, DataRaw = "1.2.3.4" };
            var typed = DnsRecordFactory.Create(ans) as ARecord;
            Assert.NotNull(typed);
            Assert.Equal(IPAddress.Parse("1.2.3.4"), typed.Address);
        }

        [Fact]
        public void Factory_Parses_AAAA_Record() {
            var ans = new DnsAnswer { Type = DnsRecordType.AAAA, DataRaw = "2001:db8::1" };
            var typed = DnsRecordFactory.Create(ans) as AAAARecord;
            Assert.NotNull(typed);
            Assert.Equal(IPAddress.Parse("2001:db8::1"), typed.Address);
        }

        [Fact]
        public void Factory_Parses_MX_Record() {
            var ans = new DnsAnswer { Type = DnsRecordType.MX, DataRaw = "10 mail.example.com." };
            var typed = DnsRecordFactory.Create(ans) as MxRecord;
            Assert.NotNull(typed);
            Assert.Equal(10, typed.Preference);
            Assert.Equal("mail.example.com", typed.Exchange);
        }

        [Fact]
        public void Factory_Parses_CNAME_Record() {
            var ans = new DnsAnswer { Type = DnsRecordType.CNAME, DataRaw = "alias.example.com." };
            var typed = DnsRecordFactory.Create(ans) as CNameRecord;
            Assert.NotNull(typed);
            Assert.Equal("alias.example.com", typed.CName);
        }

        [Fact]
        public void Factory_Parses_DNSKEY_Record() {
            var ans = new DnsAnswer { Type = DnsRecordType.DNSKEY, DataRaw = "256 3 RSASHA256 AQID" };
            var typed = DnsRecordFactory.Create(ans) as DnsKeyRecord;
            Assert.NotNull(typed);
            Assert.Equal(256, typed.Flags);
            Assert.Equal(3, typed.Protocol);
            Assert.Equal(DnsKeyAlgorithm.RSASHA256, typed.Algorithm);
            Assert.Equal("AQID", typed.PublicKey);
        }

        [Fact]
        public void Factory_Parses_DS_Record() {
            var ans = new DnsAnswer { Type = DnsRecordType.DS, DataRaw = "20326 RSASHA256 2 ABCD" };
            var typed = DnsRecordFactory.Create(ans) as DsRecord;
            Assert.NotNull(typed);
            Assert.Equal(20326, typed.KeyTag);
            Assert.Equal(DnsKeyAlgorithm.RSASHA256, typed.Algorithm);
            Assert.Equal(2, typed.DigestType);
            Assert.Equal("ABCD", typed.Digest);
        }

        [Fact]
        public void Factory_Parses_TLSA_Record() {
            var ans = new DnsAnswer { Type = DnsRecordType.TLSA, DataRaw = "3 1 1 DEADBEEF" };
            var typed = DnsRecordFactory.Create(ans) as TlsaRecord;
            Assert.NotNull(typed);
            Assert.Equal(3, typed.CertificateUsage);
            Assert.Equal(1, typed.Selector);
            Assert.Equal(1, typed.MatchingType);
            Assert.Equal("DEADBEEF", typed.AssociationData);
        }

        [Fact]
        public void Factory_Parses_NAPTR_Record() {
            var ans = new DnsAnswer { Type = DnsRecordType.NAPTR, DataRaw = "1 2 \"u\" \"sip\" \"\" example.com" };
            var typed = DnsRecordFactory.Create(ans) as NaptrRecord;
            Assert.NotNull(typed);
            Assert.Equal((ushort)1, typed.Order);
            Assert.Equal((ushort)2, typed.Preference);
            Assert.Equal("u", typed.Flags);
            Assert.Equal("sip", typed.Service);
            Assert.Equal(string.Empty, typed.RegExp);
            Assert.Equal("example.com", typed.Replacement);
        }

        [Fact]
        public void Factory_Parses_LOC_Record() {
            var ans = new DnsAnswer { Type = DnsRecordType.LOC, DataRaw = "0 0 0.000 N 0 0 0.000 E 0m 1m 10000m 10m" };
            var typed = DnsRecordFactory.Create(ans) as LocRecord;
            Assert.NotNull(typed);
            Assert.Equal(0d, typed.Latitude);
            Assert.Equal(0d, typed.Longitude);
            Assert.Equal(0d, typed.AltitudeMeters);
            Assert.Equal(1d, typed.SizeMeters);
            Assert.Equal(10000d, typed.HorizontalPrecisionMeters);
            Assert.Equal(10d, typed.VerticalPrecisionMeters);
        }
    }
}
