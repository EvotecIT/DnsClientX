using System;
using System.IO;
using System.Linq;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests the public DNS master-file parser contract.
    /// </summary>
    public class DnsZoneFileParserTests {
        /// <summary>Applies origin, compound TTL, inherited owners and generate expansion.</summary>
        [Fact]
        public void ParsesCoreMasterFileSemantics() {
            const string zone = "$ORIGIN example.com.\n" +
                                "$TTL 1h30m\n" +
                                "@ IN NS ns\n" +
                                "www 60 IN A 192.0.2.1\n" +
                                "    IN AAAA 2001:db8::1\n" +
                                "$GENERATE 1-3 host$ 300 IN A 192.0.2.$\n";

            DnsZoneFileParseResult result = DnsZoneFileParser.Parse(zone);

            Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
            Assert.Equal(6, result.Records.Count);
            Assert.Equal("example.com", result.Records[0].Name);
            Assert.Equal("ns.example.com.", result.Records[0].DataRaw);
            Assert.Equal(5400, result.Records[0].TTL);
            Assert.Equal("www.example.com", result.Records[2].Name);
            Assert.Contains(result.Records, record => record.Name == "host3.example.com" && record.DataRaw == "192.0.2.3");
        }

        /// <summary>Resolves include paths relative to the parent file and restores the parent context.</summary>
        [Fact]
        public void ParsesRelativeIncludeWithScopedOrigin() {
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string main = Path.Combine(directory, "main.zone");
            string child = Path.Combine(directory, "child.zone");
            try {
                File.WriteAllText(child, "@ IN A 192.0.2.10\nwww IN A 192.0.2.11\n");
                File.WriteAllText(main,
                    "$ORIGIN example.com.\n" +
                    "$INCLUDE child.zone delegated.example.com.\n" +
                    "after IN A 192.0.2.12\n");

                DnsZoneFileParseResult result = DnsZoneFileParser.ParseFile(main,
                    new DnsZoneFileParseOptions { AllowIncludes = true });

                Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
                Assert.Contains(result.Records, record => record.Name == "delegated.example.com");
                Assert.Contains(result.Records, record => record.Name == "www.delegated.example.com");
                Assert.Contains(result.Records, record => record.Name == "after.example.com");
            } finally {
                File.Delete(main);
                File.Delete(child);
                Directory.Delete(directory);
            }
        }

        /// <summary>Preserves TXT segment boundaries and literal parentheses inside quotes.</summary>
        [Fact]
        public void PreservesTxtPresentationSegments() {
            DnsZoneFileParseResult result = DnsZoneFileParser.Parse(
                "txt.example. IN TXT \"one (literal)\" \"two\"\n");

            DnsAnswer record = Assert.Single(result.Records);
            Assert.Equal("\"one (literal)\" \"two\"", record.DataRaw);
        }

        /// <summary>Filesystem includes are opt-in and traversal outside the configured root is rejected.</summary>
        [Fact]
        public void IncludesAreDisabledAndRootConfinedByDefault() {
            string parent = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string root = Path.Combine(parent, "zones");
            Directory.CreateDirectory(root);
            string main = Path.Combine(root, "main.zone");
            string outside = Path.Combine(parent, "outside.zone");
            try {
                File.WriteAllText(outside, "outside.example. IN A 192.0.2.9\n");
                File.WriteAllText(main, "$INCLUDE ../outside.zone\n");

                DnsZoneFileParseResult disabled = DnsZoneFileParser.ParseFile(main);
                DnsZoneFileParseResult confined = DnsZoneFileParser.ParseFile(main,
                    new DnsZoneFileParseOptions { AllowIncludes = true });

                Assert.False(disabled.Success);
                Assert.Contains(disabled.Diagnostics, item => item.Message.Contains("disabled", StringComparison.OrdinalIgnoreCase));
                Assert.False(confined.Success);
                Assert.Contains(confined.Diagnostics, item => item.Message.Contains("escapes", StringComparison.OrdinalIgnoreCase));
                Assert.Empty(confined.Records);
            } finally {
                File.Delete(main);
                File.Delete(outside);
                Directory.Delete(root);
                Directory.Delete(parent);
            }
        }

#if NET8_0_OR_GREATER
        /// <summary>Safe include mode rejects a link that lexically stays below the root but targets an outside file.</summary>
        [Fact]
        public void IncludeRootCannotBeEscapedThroughSymbolicLink() {
            string parent = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string root = Path.Combine(parent, "zones");
            Directory.CreateDirectory(root);
            string main = Path.Combine(root, "main.zone");
            string link = Path.Combine(root, "linked.zone");
            string outside = Path.Combine(parent, "outside.zone");
            try {
                File.WriteAllText(outside, "outside.example. IN A 192.0.2.9\n");
                File.CreateSymbolicLink(link, outside);
                File.WriteAllText(main, "$INCLUDE linked.zone\n");

                DnsZoneFileParseResult result = DnsZoneFileParser.ParseFile(main,
                    new DnsZoneFileParseOptions { AllowIncludes = true });

                Assert.False(result.Success);
                Assert.Contains(result.Diagnostics, item =>
                    item.Message.Contains("symbolic", StringComparison.OrdinalIgnoreCase)
                    || item.Message.Contains("reparse", StringComparison.OrdinalIgnoreCase));
                Assert.Empty(result.Records);
            } finally {
                File.Delete(main);
                File.Delete(link);
                File.Delete(outside);
                Directory.Delete(root);
                Directory.Delete(parent);
            }
        }
#endif

        /// <summary>Extreme generate endpoints and malformed TTLs return bounded diagnostics.</summary>
        [Fact]
        public void ExtremeGenerateAndTtlInputRemainBounded() {
            DnsZoneFileParseResult generated = DnsZoneFileParser.Parse(
                "$ORIGIN example.\n$GENERATE 2147483647-2147483647 host$ 60 IN A 192.0.2.1\n");
            DnsZoneFileParseResult ttl = DnsZoneFileParser.Parse(
                "$TTL 999999999999999999999999w\nwww.example. IN A 192.0.2.1\n");

            Assert.True(generated.Success, string.Join(Environment.NewLine, generated.Diagnostics));
            Assert.Single(generated.Records);
            Assert.False(ttl.Success);
            Assert.Contains(ttl.Diagnostics, item => item.Message.Contains("TTL", StringComparison.OrdinalIgnoreCase));
        }
    }
}
