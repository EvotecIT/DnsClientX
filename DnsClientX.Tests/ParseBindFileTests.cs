using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Unit tests for <see cref="BindFileParser"/> helpers.
    /// </summary>
    public class ParseBindFileTests {
        [Fact]
        public void MissingFile_ReturnsEmptyList() {
            string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            MethodInfo method = typeof(BindFileParser).GetMethod("ParseZoneFile", BindingFlags.NonPublic | BindingFlags.Static)!;
            var result = (List<DnsAnswer>)method.Invoke(null!, new object?[] { tempPath, null! })!;
            Assert.Empty(result);
        }

        [Fact]
        public void ParsesBasicZoneFile() {
            string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".zone");
            File.WriteAllText(tempPath, "$TTL 3600\n@ IN SOA ns.example.com. admin.example.com. 2024010101 7200 3600 1209600 3600\n@ IN NS ns.example.com.\nwww 600 IN A 203.0.113.10\nmail IN MX 10 mail.example.com.\n");
            MethodInfo method = typeof(BindFileParser).GetMethod("ParseZoneFile", BindingFlags.NonPublic | BindingFlags.Static)!;
            var result = (List<DnsAnswer>)method.Invoke(null!, new object?[] { tempPath, null! })!;
            File.Delete(tempPath);
            Assert.Equal(4, result.Count);
            Assert.Equal(DnsRecordType.SOA, result[0].Type);
            Assert.Equal(3600, result[0].TTL);
            Assert.Equal(DnsRecordType.A, result[2].Type);
            Assert.Equal(600, result[2].TTL);
        }

        [Fact]
        public void NegativeTtl_SkipsRecord() {
            string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".zone");
            File.WriteAllText(tempPath, "www -60 IN A 203.0.113.10\n");
            MethodInfo method = typeof(BindFileParser).GetMethod("ParseZoneFile", BindingFlags.NonPublic | BindingFlags.Static)!;
            var result = (List<DnsAnswer>)method.Invoke(null!, new object?[] { tempPath, null! })!;
            File.Delete(tempPath);
            Assert.Empty(result);
        }

        [Fact]
        public void SemicolonInQuotedText_IsNotComment() {
            string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".zone");
            File.WriteAllText(tempPath, "test IN TXT \"text;not comment\"\n");
            MethodInfo method = typeof(BindFileParser).GetMethod("ParseZoneFile", BindingFlags.NonPublic | BindingFlags.Static)!;
            var result = (List<DnsAnswer>)method.Invoke(null!, new object?[] { tempPath, null! })!;
            File.Delete(tempPath);
            Assert.Single(result);
            Assert.Equal("text;not comment", result[0].DataRaw);
        }
    }
}
