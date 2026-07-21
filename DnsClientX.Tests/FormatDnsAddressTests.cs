using System.Net;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests formatting of DNS endpoint addresses.
    /// </summary>
    public class FormatDnsAddressTests {
        private static string InvokeFormatDnsAddress(string ip) {
            var address = IPAddress.Parse(ip);
            return SystemInformation.FormatDnsAddress(address);
        }

        /// <summary>
        /// IPv4 addresses should be returned unchanged.
        /// </summary>
        [Fact]
        public void FormatIpv4_ReturnsSameString() {
            var result = InvokeFormatDnsAddress("1.1.1.1");
            Assert.Equal("1.1.1.1", result);
        }

        /// <summary>
        /// IPv6 socket addresses should retain scopes and should not use URI-only brackets.
        /// </summary>
        [Theory]
        [InlineData("2001:db8::1", "2001:db8::1")]
        [InlineData("fe80::1%12", "fe80::1%12")]
        [InlineData("::1", "::1")]
        public void FormatIpv6_PreservesSocketLiteral(string input, string expected) {
            var result = InvokeFormatDnsAddress(input);
            Assert.Equal(expected, result);
        }
    }
}
