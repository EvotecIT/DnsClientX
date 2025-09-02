using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests covering <see cref="DnsQueryException"/> construction and properties.
    /// </summary>
    public class DnsQueryExceptionTests {
        /// <summary>
        /// Error code and message are preserved; response is optional.
        /// </summary>
        [Fact]
        public void Properties_AreSet() {
            var ex = new DnsQueryException(DnsQueryErrorCode.Timeout, "timed out");
            Assert.Equal(DnsQueryErrorCode.Timeout, ex.ErrorCode);
            Assert.Equal("timed out", ex.Message);
            Assert.Null(ex.Response);
        }
    }
}
