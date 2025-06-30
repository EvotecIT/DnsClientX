using System.Reflection;
using Xunit;

namespace DnsClientX.Tests {
    public class ConvertToPtrFormatTests {
        private static string Invoke(string ip) {
            var client = new ClientX();
            MethodInfo method = typeof(ClientX).GetMethod("ConvertToPtrFormat", BindingFlags.NonPublic | BindingFlags.Instance)!;
            return (string)method.Invoke(client, new object[] { ip })!;
        }

        [Fact]
        public void TrimsAndConvertsIpv4() {
            var result = Invoke(" 1.2.3.4 ");
            Assert.Equal("4.3.2.1.in-addr.arpa", result);
        }

        [Fact]
        public void TrimsAndConvertsIpv6() {
            var result = Invoke(" 2001:db8::1 ");
            Assert.Equal("1.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.8.b.d.0.1.0.0.2.ip6.arpa", result);
        }
    }
}
