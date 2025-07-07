using System.Net;
using System.Reflection;
using Xunit;

namespace DnsClientX.Tests {
    public class IsValidDnsAddressTests {
        private static bool InvokeIsValid(string ip) {
            MethodInfo method = typeof(SystemInformation).GetMethod("IsValidDnsAddress", BindingFlags.NonPublic | BindingFlags.Static)!;
            return (bool)method.Invoke(null, new object[] { IPAddress.Parse(ip) })!;
        }

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
