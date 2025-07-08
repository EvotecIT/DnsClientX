using System.Reflection;
using Xunit;

namespace DnsClientX.Tests {
    public class ConvertToPunycodeTests {
        private static string? Invoke(string? input) {
            MethodInfo method = typeof(ClientX).GetMethod("ConvertToPunycode", BindingFlags.NonPublic | BindingFlags.Static)!;
            return (string?)method.Invoke(null, new object?[] { input });
        }

        [Fact]
        public void ReturnsOriginalForNullOrWhitespace() {
            Assert.Null(Invoke(null));
            Assert.Equal("   ", Invoke("   "));
        }

        [Fact]
        public void ReturnsOriginalForInvalidDomain() {
            const string invalid = "a^b.com";
            Assert.Equal(invalid, Invoke(invalid));
        }

        [Fact]
        public void ReturnsOriginalForInvalidIdn() {
            const string invalidIdn = "\uD83D\uDE00.com";
            Assert.Equal(invalidIdn, Invoke(invalidIdn));
        }

        [Fact]
        public void PreservesTrailingDot() {
            const string input = "example.com.";
            Assert.Equal(input, Invoke(input));
        }
    }
}
