using Xunit;

namespace DnsClientX.Tests {
    public class DnsQuestionTests {
        [Fact]
        public void SettingNameStripsTrailingDotButKeepsOriginal() {
            var q = new DnsQuestion { Name = "example.com." };
            Assert.Equal("example.com", q.Name);
            Assert.Equal("example.com.", q.OriginalName);
        }

        [Fact]
        public void SettingNameWithoutDotUnchanged() {
            var q = new DnsQuestion { Name = "example.com" };
            Assert.Equal("example.com", q.Name);
            Assert.Equal("example.com", q.OriginalName);
        }
    }
}
