using System;
using System.Linq;
using Xunit;

namespace DnsClientX.Tests {
    public class TxtRecordParsingTests {
        [Fact]
        public void TestGoogleJsonNewlineDelimited() {
            var answer = new DnsAnswer {
                Type = DnsRecordType.TXT,
                DataRaw = "v=spf1 include:_spf.google.com ~all\ngoogle-site-verification=xxxxxxxx"
            };
            var expected = new[] { "\"v=spf1 include:_spf.google.com ~all\"", "\"google-site-verification=xxxxxxxx\"" };
            Assert.Equal(expected, answer.DataStrings);
        }

        [Fact]
        public void TestGoogleJsonNewlineDelimitedWithBlankLines() {
            var answer = new DnsAnswer {
                Type = DnsRecordType.TXT,
                DataRaw = "\nv=spf1\n\ngoogle-site-verification=abc\n  \n"
            };
            var expected = new[] { "\"v=spf1\"", "\"google-site-verification=abc\"" };
            Assert.Equal(expected, answer.DataStrings);
        }

        [Fact]
        public void TestGoogleJsonSingleEntryNoNewline() {
            var answer = new DnsAnswer {
                Type = DnsRecordType.TXT,
                DataRaw = "google-site-verification=yyyyyyyy"
            };
            var expected = new[] { "\"google-site-verification=yyyyyyyy\"" };
            Assert.Equal(expected, answer.DataStrings);
        }

        [Fact]
        public void TestGoogleJsonConcatenatedWithoutNewline() {            // This scenario, without quotes and newlines, is treated as a single TXT record.
            // The Google-specific logic is only for newline-separated values.
            var answer = new DnsAnswer {
                Type = DnsRecordType.TXT,
                DataRaw = "item1item2item3"
            };
            var expected = new[] { "\"item1item2item3\"" };
            Assert.Equal(expected, answer.DataStrings);
        }

        [Fact]
        public void TestWireFormatConcatenatedDoubleQuotes() {
            var answer = new DnsAnswer {
                Type = DnsRecordType.TXT,
                DataRaw = "\"v=spf1\"\"record2\"\"\"\"record3 with space\""
            };
            var expected = new[] { "\"v=spf1\"", "\"record2\"", "\"\"", "\"record3 with space\"" };
            Assert.Equal(expected, answer.DataStrings);
        }

        [Fact]
        public void TestWireFormatConcatenatedSpacedQuotes() {
            var answer = new DnsAnswer {
                Type = DnsRecordType.TXT,
                DataRaw = "\"v=spf1\" \"record2\" \"record3 with space\""
            };
            var expected = new[] { "\"v=spf1\"", "\"record2\"", "\"record3 with space\"" };
            Assert.Equal(expected, answer.DataStrings);
        }

        [Fact]
        public void TestSingleStandardQuotedTxt() {
            var answer = new DnsAnswer {
                Type = DnsRecordType.TXT,
                DataRaw = "\"single record text\""
            };
            var expected = new[] { "\"single record text\"" };
            Assert.Equal(expected, answer.DataStrings);
        }

        [Fact]
        public void TestEmptyDataRaw() {
            var answer = new DnsAnswer {
                Type = DnsRecordType.TXT,
                DataRaw = ""
            };
            var expected = new[] { "\"\"" };
            Assert.Equal(expected, answer.DataStrings);
        }

        [Fact]
        public void TestNullDataRaw() {
            var answer = new DnsAnswer {
                Type = DnsRecordType.TXT,
                DataRaw = null
            };
            var expected = Array.Empty<string>();
            Assert.Equal(expected, answer.DataStrings);
        }

        [Fact]
        public void TestTxtDataRawOnlyWhitespace() {
            var answer = new DnsAnswer {
                Type = DnsRecordType.TXT,
                DataRaw = "   "
            };
            var expected = new[] { "\"   \"" };
            Assert.Equal(expected, answer.DataStrings);
        }
    }
}
