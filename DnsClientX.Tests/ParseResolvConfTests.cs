using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for the internal <c>SystemInformation.ParseResolvConf</c> method.
    /// </summary>
    public class ParseResolvConfTests {
        /// <summary>
        /// Ensures parsing a missing file results in an empty list of servers.
        /// </summary>
        [Fact]
        public void MissingFile_ReturnsEmptyList() {
            string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
            MethodInfo method = typeof(SystemInformation).GetMethod("ParseResolvConf", BindingFlags.NonPublic | BindingFlags.Static)!;
            var result = (List<string>)method.Invoke(null!, new object?[] { tempPath, null! })!;
            Assert.Empty(result);
        }
    }
}
