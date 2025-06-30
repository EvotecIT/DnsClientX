using System.Net;
using System.Reflection;
using Xunit;

namespace DnsClientX.Tests {
    public class FormatDnsAddressTests {
        private static string InvokeFormatDnsAddress(string ip) {
            MethodInfo method = typeof(SystemInformation).GetMethod("FormatDnsAddress", BindingFlags.NonPublic | BindingFlags.Static)!;
            var address = IPAddress.Parse(ip);
            return (string)method.Invoke(null, new object[] { address })!;
        }

        [Fact]
        public void FormatIpv4_ReturnsSameString() {
            var result = InvokeFormatDnsAddress("1.1.1.1");
            Assert.Equal("1.1.1.1", result);
        }

        [Theory]
        [InlineData("2001:db8::1", "[2001:db8::1]")]
        [InlineData("fe80::1%12", "[fe80::1]")]
        public void FormatIpv6_RemovesZoneAndAddsBrackets(string input, string expected) {
            var result = InvokeFormatDnsAddress(input);
            Assert.Equal(expected, result);
        }
    }
}
