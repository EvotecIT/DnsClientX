using System;
using System.IO;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for the <see cref="BindFileParser"/> helper.
    /// </summary>
    public class BindFileParserTests {
        /// <summary>
        /// Ensures that zone files are parsed and default values are applied.
        /// </summary>
        [Fact]
        public void ParseZoneFile_ReadsRecordsAndAppliesDefaults() {
            string file = Path.GetTempFileName();
            File.WriteAllLines(file, new[] {
                "$TTL 1800",
                "example.com. IN A 1.1.1.1",
                "www 60 IN CNAME example.com.",
                "badttl -1 IN A 1.1.1.1"
            });

            var records = BindFileParser.ParseZoneFile(file);

            Assert.Equal(2, records.Count);
            Assert.Equal("example.com", records[0].Name);
            Assert.Equal(1800, records[0].TTL);
            Assert.Equal(DnsRecordType.A, records[0].Type);
            Assert.Equal("1.1.1.1", records[0].DataRaw);
            Assert.Equal("www", records[1].Name);
            Assert.Equal(60, records[1].TTL);
            Assert.Equal(DnsRecordType.CNAME, records[1].Type);
            Assert.Equal("example.com.", records[1].DataRaw);
        }

        /// <summary>
        /// Verifies that multiline TXT records are combined into a single value.
        /// </summary>
        [Fact]
        public void ParseZoneFile_CombinesMultiLineTxtRecords() {
            string file = Path.GetTempFileName();
            File.WriteAllLines(file, new[] {
                "example.com. IN TXT \"line1",
                "line2\""
            });

            var records = BindFileParser.ParseZoneFile(file);

            Assert.Single(records);
            Assert.Equal("example.com", records[0].Name);
            Assert.Equal("line1 line2", records[0].DataRaw);
        }

        /// <summary>
        /// Ensures that escaped newlines in TXT records are converted to actual newline characters.
        /// </summary>
        [Fact]
        public void ParseZoneFile_ReplacesEscapedNewlinesInTxtRecords() {
            string file = Path.GetTempFileName();
            File.WriteAllLines(file, new[] {
                "example.com. IN TXT \"line1\\nline2\""
            });

            var records = BindFileParser.ParseZoneFile(file);

            Assert.Single(records);
            Assert.Equal("example.com", records[0].Name);
            Assert.Equal("line1\nline2", records[0].DataRaw);
        }

        /// <summary>
        /// Confirms that TTL suffixes such as <c>h</c> and <c>m</c> are correctly parsed.
        /// </summary>
        [Fact]
        public void ParseZoneFile_ParsesTtlSuffixes() {
            string file = Path.GetTempFileName();
            File.WriteAllLines(file, new[] {
                "$TTL 1h",
                "example.com. IN A 1.1.1.1",
                "www 30m IN CNAME example.com.",
                "mail 7200 IN A 2.2.2.2"
            });

            var records = BindFileParser.ParseZoneFile(file);

            Assert.Equal(3, records.Count);
            Assert.Equal(3600, records[0].TTL);
            Assert.Equal(1800, records[1].TTL);
            Assert.Equal(7200, records[2].TTL);
        }

        /// <summary>
        /// Verifies that parenthesized records are joined together across lines.
        /// </summary>
        [Fact]
        public void ParseZoneFile_JoinsParenthesizedRecords() {
            string file = Path.GetTempFileName();
            File.WriteAllLines(file, new[] {
                "example.com. IN SOA ns.example.com. admin.example.com. (",
                "  2024010101",
                "  7200",
                "  3600",
                "  1209600",
                "  3600 )"
            });

            var records = BindFileParser.ParseZoneFile(file);

            Assert.Single(records);
            Assert.Equal(DnsRecordType.SOA, records[0].Type);
            Assert.Equal("ns.example.com. admin.example.com. 2024010101 7200 3600 1209600 3600", records[0].DataRaw);
        }

        /// <summary>
        /// Ensures that a warning is produced when a negative TTL directive is encountered.
        /// </summary>
        [Fact]
        public void ParseZoneFile_WarnsOnNegativeTtlDirective() {
            string file = Path.GetTempFileName();
            File.WriteAllLines(file, new[] {
                "$TTL -1",
                "example.com. IN A 1.1.1.1"
            });

            var messages = new System.Collections.Generic.List<string>();
            var records = BindFileParser.ParseZoneFile(file, m => messages.Add(m));

            Assert.Single(records);
            Assert.Contains(messages, m => m.Contains("negative"));
        }
    }
}
