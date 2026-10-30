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
        /// <summary>
        /// Ensures the parser returns an empty collection when the zone file is missing.
        /// </summary>
        [Fact]
        public void MissingFile_ReturnsEmptyList() {
            string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            MethodInfo method = typeof(BindFileParser).GetMethod("ParseZoneFile", BindingFlags.NonPublic | BindingFlags.Static)!;
            var result = (List<DnsAnswer>)method.Invoke(null!, new object?[] { tempPath, null! })!;
            Assert.Empty(result);
        }

        /// <summary>
        /// Verifies that semicolons inside quoted text are not treated as comments.
        /// </summary>
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
