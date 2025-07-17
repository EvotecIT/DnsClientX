using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace DnsClientX.Tests {
    public class ParseResolvConfTests {
        [Fact]
        public void MissingFile_ReturnsEmptyList() {
            string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
            MethodInfo method = typeof(SystemInformation).GetMethod("ParseResolvConf", BindingFlags.NonPublic | BindingFlags.Static)!;
            var result = (List<string>)method.Invoke(null, new object?[] { tempPath, null })!;
            Assert.Empty(result);
        }
    }
}
