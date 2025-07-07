using System;
using Xunit;

namespace DnsClientX.Tests {
    public class DnsKeyAlgorithmExtensionsTests {
        [Fact]
        public void FromValue_ReturnsEnum() {
            var result = DnsKeyAlgorithmExtensions.FromValue(8);
            Assert.Equal(DnsKeyAlgorithm.RSASHA256, result);
        }

        [Fact]
        public void FromValue_Invalid_Throws() {
            Assert.Throws<ArgumentException>(() => DnsKeyAlgorithmExtensions.FromValue(999));
        }
    }
}
