using System.Net;
using DnsClientX;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests the <see cref="DnsRecordFactory"/> producing strongly typed records.
    /// </summary>
    public class TypedRecordsTests {
        /// <summary>
        /// Parses an A record into an <see cref="ARecord"/> instance.
        /// </summary>
        [Fact]
        public void Factory_Parses_A_Record() {
            var ans = new DnsAnswer { Type = DnsRecordType.A, DataRaw = "1.2.3.4" };
            var typed = DnsRecordFactory.Create(ans) as ARecord;
            Assert.NotNull(typed);
            Assert.Equal(IPAddress.Parse("1.2.3.4"), typed.Address);
        }

        /// <summary>
        /// Parses an AAAA record into an <see cref="AAAARecord"/> instance.
        /// </summary>
        [Fact]
        public void Factory_Parses_AAAA_Record() {
            var ans = new DnsAnswer { Type = DnsRecordType.AAAA, DataRaw = "2001:db8::1" };
            var typed = DnsRecordFactory.Create(ans) as AAAARecord;
            Assert.NotNull(typed);
            Assert.Equal(IPAddress.Parse("2001:db8::1"), typed.Address);
        }

        /// <summary>
        /// Parses an MX record into an <see cref="MxRecord"/> instance.
        /// </summary>
        [Fact]
        public void Factory_Parses_MX_Record() {
            var ans = new DnsAnswer { Type = DnsRecordType.MX, DataRaw = "10 mail.example.com." };
            var typed = DnsRecordFactory.Create(ans) as MxRecord;
            Assert.NotNull(typed);
            Assert.Equal(10, typed.Preference);
            Assert.Equal("mail.example.com", typed.Exchange);
        }

        /// <summary>
        /// Parses a CNAME record into a <see cref="CNameRecord"/> instance.
        /// </summary>
        [Fact]
        public void Factory_Parses_CNAME_Record() {
            var ans = new DnsAnswer { Type = DnsRecordType.CNAME, DataRaw = "alias.example.com." };
            var typed = DnsRecordFactory.Create(ans) as CNameRecord;
            Assert.NotNull(typed);
            Assert.Equal("alias.example.com", typed.CName);
        }

        /// <summary>
        /// Parses a DNSKEY record into a <see cref="DnsKeyRecord"/> instance.
        /// </summary>
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

        /// <summary>
        /// Parses a DS record into a <see cref="DsRecord"/> instance.
        /// </summary>
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

        /// <summary>
        /// Parses a TLSA record into a <see cref="TlsaRecord"/> instance.
        /// </summary>
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

        /// <summary>
        /// Parses a NAPTR record into a <see cref="NaptrRecord"/> instance.
        /// </summary>
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

        /// <summary>
        /// Parses a LOC record into a <see cref="LocRecord"/> instance.
        /// </summary>
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

        /// <summary>
        /// Parses a DMARC TXT record into a <see cref="DmarcRecord"/> instance.
        /// </summary>
        [Fact]
        public void Factory_Parses_Dmarc_Record() {
            var ans = new DnsAnswer { Type = DnsRecordType.TXT, DataRaw = "v=DMARC1; p=none; rua=mailto:example@example.com" };
            var typed = DnsRecordFactory.Create(ans) as DmarcRecord;
            Assert.NotNull(typed);
            Assert.Equal("DMARC1", typed.Tags["v"]);
            Assert.Equal("none", typed.Tags["p"]);
        }

        /// <summary>
        /// Parses a DKIM TXT record into a <see cref="DkimRecord"/> instance.
        /// </summary>
        [Fact]
        public void Factory_Parses_Dkim_Record() {
            var ans = new DnsAnswer { Type = DnsRecordType.TXT, DataRaw = "v=DKIM1; k=rsa; p=ABC" };
            var typed = DnsRecordFactory.Create(ans) as DkimRecord;
            Assert.NotNull(typed);
            Assert.Equal("DKIM1", typed.Tags["v"]);
            Assert.Equal("rsa", typed.Tags["k"]);
        }

        /// <summary>
        /// Parses an SPF record into a <see cref="SpfRecord"/> instance.
        /// </summary>
        [Fact]
        public void Factory_Parses_Spf_Record() {
            var ans = new DnsAnswer { Type = DnsRecordType.SPF, DataRaw = "v=spf1 include:example.com -all" };
            var typed = DnsRecordFactory.Create(ans) as SpfRecord;
            Assert.NotNull(typed);
            Assert.Equal("v=spf1", typed.Version);
            Assert.Contains("include:example.com", typed.Mechanisms);
        }

        /// <summary>
        /// Parses a TXT record containing key=value pairs.
        /// </summary>
        [Fact]
        public void Factory_Parses_KeyValue_Record() {
            var ans = new DnsAnswer { Type = DnsRecordType.TXT, DataRaw = "foo=bar baz=qux" };
            var typed = DnsRecordFactory.Create(ans) as KeyValueTxtRecord;
            Assert.NotNull(typed);
            Assert.Equal("bar", typed.Tags["foo"]);
            Assert.Equal("qux", typed.Tags["baz"]);
        }

        /// <summary>
        /// Ensures TXT parsing can be disabled for typed TXT records.
        /// </summary>
        [Fact]
        public void Factory_Respects_TypedTxtAsTxt() {
            var ans = new DnsAnswer { Type = DnsRecordType.TXT, DataRaw = "v=spf1 -all" };
            var typed = DnsRecordFactory.Create(ans, typedTxtAsTxt: true);
            Assert.IsType<TxtRecord>(typed);
        }

        /// <summary>
        /// Parses a domain verification TXT record.
        /// </summary>
        [Fact]
        public void Factory_Parses_DomainVerification_Record() {
            var ans = new DnsAnswer { Type = DnsRecordType.TXT, DataRaw = "openai-domain-verification=abc" };
            var typed = DnsRecordFactory.Create(ans) as DomainVerificationRecord;
            Assert.NotNull(typed);
            Assert.Equal("openai-domain-verification", typed.Provider);
            Assert.Equal("abc", typed.Token);
        }

        /// <summary>
        /// TxtRecord exposes the concatenated text via the Value property.
        /// </summary>
        [Fact]
        public void TxtRecord_Returns_Concatenated_Value() {
            var record = new TxtRecord(new[] { "foo", "bar" });
            Assert.Equal("foobar", record.Value);
        }
    }
}
