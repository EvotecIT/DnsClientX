using System;
using System.IO;
using Xunit;

namespace DnsClientX.Tests {
    public class BindFileParserTests {
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
            Assert.Contains(messages, m => m.Contains("negative value"));
        }
    }
}
