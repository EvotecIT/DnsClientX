using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests ensuring <see cref="DnsQuestion"/> and <see cref="DnsAnswer"/> preserve the original name value.
    /// </summary>
    public class OriginalNameTests {
        /// <summary>
        /// Verifies that setting <see cref="DnsQuestion.Name"/> also populates <see cref="DnsQuestion.OriginalName"/>.
        /// </summary>
        [Fact]
        public void QuestionSetterSetsOriginalName() {
            DnsQuestion q = new() { Name = "example.com." };
            Assert.Equal("example.com.", q.OriginalName);
            Assert.Equal("example.com", q.Name);
        }

        /// <summary>
        /// Verifies that setting <see cref="DnsAnswer.Name"/> also populates <see cref="DnsAnswer.OriginalName"/>.
        /// </summary>
        [Fact]
        public void AnswerSetterSetsOriginalName() {
            DnsAnswer a = new() { Name = "example.net." };
            Assert.Equal("example.net.", a.OriginalName);
            Assert.Equal("example.net", a.Name);
        }
    }
}
