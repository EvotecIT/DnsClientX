using System;
using System.IO;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests resolv.conf discovery behavior.
    /// </summary>
    public class ParseResolvConfTests {
        /// <summary>Ensures parsing a missing file produces an explicit empty result.</summary>
        [Fact]
        public void MissingFile_ReturnsEmptyConfiguration() {
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            SystemDnsConfiguration result = SystemInformation.ParseResolvConf(path);
            Assert.False(result.HasDnsServers);
            Assert.Equal(SystemDnsDiscoverySource.None, result.Source);
        }

        /// <summary>Parses resolver order, loopback servers, search domains and ndots.</summary>
        [Fact]
        public void ParsesResolverSearchAndOptions() {
            string path = Path.GetTempFileName();
            try {
                File.WriteAllText(path,
                    "nameserver 127.0.0.53\n" +
                    "nameserver ::1 # local resolver\n" +
                    "search corp.example example.test\n" +
                    "options timeout:2 ndots:3 attempts:2\n");

                SystemDnsConfiguration result = SystemInformation.ParseResolvConf(path);

                Assert.Equal(new[] { "127.0.0.53", "::1" }, result.DnsServers);
                Assert.Equal(new[] { "corp.example", "example.test" }, result.SearchDomains);
                Assert.Equal(3, result.Ndots);
                Assert.Equal(SystemDnsDiscoverySource.ResolvConf, result.Source);
            } finally {
                File.Delete(path);
            }
        }
    }
}
