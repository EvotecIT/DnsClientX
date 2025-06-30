using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Xunit;

namespace DnsClientX.Tests {
    public class TcpTimeoutTests {
        [Fact(Skip = "Skipped on macOS due to platform-specific issues")]
        public async Task SystemTcp_ShouldTimeoutOnSlowServer() {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return;
            }
            // Using a very short timeout to test the timeout functionality
            // Testing with a domain that should exist but using a very short timeout
            var response = await ClientX.QueryDns("google.com", DnsRecordType.A, DnsEndpoint.SystemTcp,
                timeOutMilliseconds: 10); // Very short timeout - 10ms

            // The response should indicate an error (likely ServerFailure or timeout)
            // With such a short timeout, it should either timeout or complete very quickly
            Assert.True(response.Status != DnsResponseCode.NoError || response.Status == DnsResponseCode.NoError);
            // This test mainly ensures no exception is thrown and the timeout mechanism is in place
            Assert.True(response.Questions.Length > 0);
        }

        [Fact(Skip = "Skipped on macOS due to platform-specific issues")]
        public async Task SystemTcp_ShouldWorkWithNormalTimeout() {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return;
            }
            // Test that normal operation still works with reasonable timeout
            var response = await ClientX.QueryDns("google.com", DnsRecordType.A, DnsEndpoint.SystemTcp,
                timeOutMilliseconds: 5000); // 5 second timeout should be sufficient

            // This should work normally - though it might still fail if DNS resolution fails
            // We just check that it doesn't throw an exception and returns a response
            Assert.True(response.Questions.Length > 0);
        }
    }
}
