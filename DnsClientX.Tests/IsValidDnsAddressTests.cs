using System.Net;
using System.Reflection;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests the internal <c>SystemInformation.IsValidDnsAddress</c> method via reflection.
    /// </summary>
    public class IsValidDnsAddressTests {
        private static bool InvokeIsValid(string ip) {
            MethodInfo method = typeof(SystemInformation).GetMethod("IsValidDnsAddress", BindingFlags.NonPublic | BindingFlags.Static)!;
            return (bool)method.Invoke(null, new object[] { IPAddress.Parse(ip) })!;
        }

        /// <summary>
        /// Validates a variety of addresses to ensure the helper behaves as expected.
        /// </summary>
        /// <param name="ip">The address to check.</param>
        /// <param name="expected">Whether the address should be considered valid.</param>
        [Theory]
        [InlineData("1.1.1.1", true)]
        [InlineData("169.254.0.1", false)]
        [InlineData("127.0.0.1", false)]
        [InlineData("2001:db8::1", true)]
        [InlineData("fe80::1", false)]
        public void ValidatesAddresses(string ip, bool expected) {
            bool result = InvokeIsValid(ip);
            Assert.Equal(expected, result);
        }
    }
}
