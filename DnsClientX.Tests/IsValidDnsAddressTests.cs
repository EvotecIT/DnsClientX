using System.Net;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests whether an operating-system resolver address can be used by a DNS socket.
    /// </summary>
    public class IsValidDnsAddressTests {
        private static bool InvokeIsValid(string ip) {
            return SystemInformation.IsUsableDnsAddress(IPAddress.Parse(ip));
        }

        /// <summary>
        /// Validates a variety of addresses to ensure the helper behaves as expected.
        /// </summary>
        /// <param name="ip">The address to check.</param>
        /// <param name="expected">Whether the address should be considered valid.</param>
        [Theory]
        [InlineData("1.1.1.1", true)]
        [InlineData("169.254.0.1", true)]
        [InlineData("127.0.0.1", true)]
        [InlineData("2001:db8::1", true)]
        [InlineData("fe80::1", true)]
        [InlineData("0.0.0.0", false)]
        [InlineData("::", false)]
        [InlineData("ff02::fb", false)]
        public void ValidatesAddresses(string ip, bool expected) {
            bool result = InvokeIsValid(ip);
            Assert.Equal(expected, result);
        }
    }
}
