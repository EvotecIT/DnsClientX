using Xunit;

namespace DnsClientX.Tests {
    public class DnsQueryExceptionTests {
        [Fact]
        public void Properties_AreSet() {
            var ex = new DnsQueryException(DnsQueryErrorCode.Timeout, "timed out");
            Assert.Equal(DnsQueryErrorCode.Timeout, ex.ErrorCode);
            Assert.Equal("timed out", ex.Message);
            Assert.Null(ex.Response);
        }
    }
}

