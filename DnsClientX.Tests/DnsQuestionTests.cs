using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for the <see cref="DnsQuestion"/> model.
    /// </summary>
    public class DnsQuestionTests {
        /// <summary>
        /// Setting a trailing dot strips it from the stored name but preserves the original value.
        /// </summary>
        [Fact]
        public void SettingNameStripsTrailingDotButKeepsOriginal() {
            var q = new DnsQuestion { Name = "example.com." };
            Assert.Equal("example.com", q.Name);
            Assert.Equal("example.com.", q.OriginalName);
        }

        /// <summary>
        /// Names without a trailing dot should remain unchanged.
        /// </summary>
        [Fact]
        public void SettingNameWithoutDotUnchanged() {
            var q = new DnsQuestion { Name = "example.com" };
            Assert.Equal("example.com", q.Name);
            Assert.Equal("example.com", q.OriginalName);
        }
    }
}
