using System.Reflection;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for the internal ConvertToPunycode helper.
    /// </summary>
    public class ConvertToPunycodeTests {
        private static string? Invoke(string? input) {
            MethodInfo method = typeof(ClientX).GetMethod("ConvertToPunycode", BindingFlags.NonPublic | BindingFlags.Static)!;
            return (string?)method.Invoke(null, new object?[] { input });
        }

        /// <summary>
        /// Null or whitespace inputs should be returned unchanged.
        /// </summary>
        [Fact]
        public void ReturnsOriginalForNullOrWhitespace() {
            Assert.Null(Invoke(null));
            Assert.Equal("   ", Invoke("   "));
        }

        /// <summary>
        /// Invalid domain names are left untouched.
        /// </summary>
        [Fact]
        public void ReturnsOriginalForInvalidDomain() {
            const string invalid = "a^b.com";
            Assert.Equal(invalid, Invoke(invalid));
        }

        /// <summary>
        /// Inputs containing invalid IDN characters should be returned unchanged.
        /// </summary>
        [Fact]
        public void ReturnsOriginalForInvalidIdn() {
            const string invalidIdn = "\uD83D\uDE00.com";
            Assert.Equal(invalidIdn, Invoke(invalidIdn));
        }

        /// <summary>
        /// Trailing dots should be preserved when converting.
        /// </summary>
        [Fact]
        public void PreservesTrailingDot() {
            const string input = "example.com.";
            Assert.Equal(input, Invoke(input));
        }
    }
}
