using Xunit;

namespace DnsClientX.Tests {
    public class OriginalNameTests {
        [Fact]
        public void QuestionSetterSetsOriginalName() {
            DnsQuestion q = new() { Name = "example.com." };
            Assert.Equal("example.com.", q.OriginalName);
            Assert.Equal("example.com", q.Name);
        }

        [Fact]
        public void AnswerSetterSetsOriginalName() {
            DnsAnswer a = new() { Name = "example.net." };
            Assert.Equal("example.net.", a.OriginalName);
            Assert.Equal("example.net", a.Name);
        }
    }
}
