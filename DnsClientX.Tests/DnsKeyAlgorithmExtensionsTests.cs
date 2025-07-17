using System;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for <see cref="DnsKeyAlgorithmExtensions"/> utilities.
    /// </summary>
    public class DnsKeyAlgorithmExtensionsTests {
        /// <summary>
        /// Ensures known algorithm values are correctly parsed.
        /// </summary>
        [Fact]
        public void FromValue_ReturnsEnum() {
            var result = DnsKeyAlgorithmExtensions.FromValue(8);
            Assert.Equal(DnsKeyAlgorithm.RSASHA256, result);
        }

        /// <summary>
        /// Ensures invalid values throw an exception.
        /// </summary>
        [Fact]
        public void FromValue_Invalid_Throws() {
            Assert.Throws<ArgumentException>(() => DnsKeyAlgorithmExtensions.FromValue(999));
        }
    }
}
