using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for the <see cref="DnsClientException"/> type.
    /// </summary>
    public class DnsClientExceptionTests {
        /// <summary>
        /// Ensures the exception message includes details about the endpoint.
        /// </summary>
        [Fact]
        public void MessageIncludesEndpointDetails() {
            var response = new DnsResponse {
                Questions = new[] {
                    new DnsQuestion {
                        Name = "example.com",
                        Type = DnsRecordType.A,
                        HostName = "8.8.8.8",
                        Port = 53,
                        RequestFormat = DnsRequestFormat.DnsOverHttps
                    }
                },
                Status = DnsResponseCode.ServerFailure
            };

            var ex = new DnsClientException("error", response);
            Assert.Contains("error", ex.Message);
            Assert.Contains("8.8.8.8", ex.Message);
            Assert.Contains("53", ex.Message);
            Assert.Contains(nameof(DnsRequestFormat.DnsOverHttps), ex.Message);
        }

        /// <summary>
        /// Ensures the message remains unchanged when no endpoint details are provided.
        /// </summary>
        [Fact]
        public void MessageWithoutEndpointDetailsUnchanged() {
            var response = new DnsResponse { Status = DnsResponseCode.ServerFailure };

            var ex = new DnsClientException("oops", response);
            Assert.Equal("oops", ex.Message);
        }
    }
}
