using System.Globalization;
using System.Threading;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for helper methods on <see cref="DnsAnswer"/>.
    /// </summary>
    public class DnsAnswerTests {
        /// <summary>
        /// Ensures conversion to a string array returns an empty array when data is null.
        /// </summary>
        [Fact]
        public void ConvertToMultiString_NullData_ReturnsEmptyArray() {
            var answer = new DnsAnswer {
                Name = "example.com",
                Type = DnsRecordType.TXT,
                TTL = 0,
                DataRaw = null!
            };

            Assert.Empty(answer.DataStrings);
        }

        /// <summary>
        /// Ensures that NS record data is parsed consistently regardless of culture.
        /// </summary>
        [Theory]
        [InlineData("en-US")]
        [InlineData("tr-TR")]
        public void ConvertData_NsRecord_ConsistentAcrossCultures(string culture) {
            var answer = new DnsAnswer {
                Name = "example.com",
                Type = DnsRecordType.NS,
                TTL = 3600,
                DataRaw = "EXAMPLEI.COM"
            };

            var original = Thread.CurrentThread.CurrentCulture;
            try {
                Thread.CurrentThread.CurrentCulture = new CultureInfo(culture);
                Assert.Equal("examplei.com", answer.Data);
            } finally {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }

        /// <summary>
        /// Ensures TXT concatenated output flattens line breaks into a single script-friendly string.
        /// </summary>
        [Fact]
        public void TxtConcatenatedData_FlattensTxtLines() {
            var answer = new DnsAnswer {
                Name = "example.com",
                Type = DnsRecordType.TXT,
                TTL = 300,
                DataRaw = "line1\nline2"
            };

            Assert.Equal("line1line2", answer.TxtConcatenatedData);
        }

        /// <summary>
        /// Ensures character-strings within one TXT RDATA are concatenated without fabricating multiple records.
        /// </summary>
        [Fact]
        public void TxtData_ConcatenatesCharacterStringsWithinOneRecord() {
            var answer = new DnsAnswer {
                Name = "example.com",
                Type = DnsRecordType.TXT,
                TTL = 300,
                DataRaw = "\"v=spf1 include:\" \"example.com -all\""
            };

            Assert.Equal("v=spf1 include:example.com -all", answer.Data);
            Assert.Equal(2, answer.DataStrings.Length);
        }

        /// <summary>Decoded and typed TXT preserve content and character-string boundaries.</summary>
        [Theory]
        [InlineData("\"A\" \" \" \"B\"", "A B", 3)]
        [InlineData("\"\"\"  Alpha  \"\"\"", "  Alpha  ", 3)]
        [InlineData("\"A\\010\\010B\"", "A\n\nB", 1)]
        [InlineData("\"a\\\"\"", "a\"", 1)]
        [InlineData("\"a\\\\\" \"b\"", "a\\b", 2)]
        [InlineData("\"A\\013\\010B\"", "A\r\nB", 1)]
        [InlineData("  Alpha  ", "  Alpha  ", 1)]
        public void TxtPayloadIsPreserved(string raw, string expected, int chunks) {
            var answer = new DnsAnswer { Type = DnsRecordType.TXT, DataRaw = raw };
            Assert.Equal(expected, answer.Data);
            Assert.Equal(expected, string.Concat(answer.DataStringsEscaped));
            Assert.Equal(chunks, answer.DataStrings.Length);
            Assert.Equal(chunks, answer.DataStringsEscaped.Length);
            Assert.Equal(expected, Assert.IsType<TxtRecord>(answer.TypedRecord).Text);
            Assert.Equal(raw, answer.DataRaw);
        }

        /// <summary>Application and encoded RDATA are case-sensitive even when DNS names are not.</summary>
        [Theory]
        [InlineData(DnsRecordType.RRSIG, "A 13 2 3600 20300101000000 20200101000000 12345 EXAMPLE. AQIDAbCd+/==")]
        [InlineData(DnsRecordType.URI, "10 1 \"https://Example.com/CaseSensitive?Token=AbCd\"")]
        [InlineData(DnsRecordType.HINFO, "\"ARM64\" \"MacOS\"")]
        [InlineData((DnsRecordType)65400, "\\# 3 ABCDef")]
        public void OpaquePayloadKeepsCase(DnsRecordType type, string raw) {
            var answer = new DnsAnswer { Type = type, DataRaw = raw };
            Assert.Equal(raw, answer.Data);
            Assert.Equal(raw, Assert.IsType<UnknownRecord>(answer.TypedRecord).Data);
        }

        /// <summary>Typed NAPTR respects quoted spaces, escaped quotes, and the root replacement.</summary>
        [Fact]
        public void NaptrFieldsRetainQuotedContent() {
            var answer = new DnsAnswer { Type = DnsRecordType.NAPTR,
                DataRaw = "10 20 \"u\" \"E2U+sip\" \"!^.*$!sip:First Last\\\"@example.com!\" ." };
            var record = Assert.IsType<NaptrRecord>(answer.TypedRecord);
            Assert.Equal("!^.*$!sip:First Last\"@example.com!", record.RegExp);
            Assert.Equal(".", record.Replacement);
            Assert.Equal("E2U+sip", record.Service);
        }

        /// <summary>Binary provider representations preserve capture substitutions, quotes, and opaque octets.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void BinaryNaptrRetainsQuotedRegexp(bool hexadecimal) {
            byte[] regexp = System.Text.Encoding.ASCII.GetBytes("!^(.*)$!sip:\\1\"@example.com!").Concat(new byte[] { 255 }).ToArray();
            byte[] rdata = new byte[] { 0, 10, 0, 20, 1, (byte)'u', 0, (byte)regexp.Length }.Concat(regexp).Concat(new byte[] { 0 }).ToArray();
            var answer = new DnsAnswer { Type = DnsRecordType.NAPTR, DataRaw = hexadecimal
                ? "\\# " + rdata.Length + " " + string.Join(" ", rdata.Select(value => value.ToString("X2")))
                : Convert.ToBase64String(rdata) };
            var record = Assert.IsType<NaptrRecord>(answer.TypedRecord);
            Assert.Equal("!^(.*)$!sip:\\1\"@example.com!\u00ff", record.RegExp);
            Assert.Equal(".", record.Replacement);
        }
    }
}
