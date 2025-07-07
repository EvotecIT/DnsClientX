using Xunit;

namespace DnsClientX.Tests {
    public class DnsClientExceptionTests {
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

        [Fact]
        public void MessageWithoutEndpointDetailsUnchanged() {
            var response = new DnsResponse { Status = DnsResponseCode.ServerFailure };

            var ex = new DnsClientException("oops", response);
            Assert.Equal("oops", ex.Message);
        }
    }
}
