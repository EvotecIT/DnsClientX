#if NET8_0_OR_GREATER
using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace DnsClientX.Tests {
    /// <summary>
    /// Opt-in live checks for modern DNS transports that are supported by the core package on modern runtimes.
    /// </summary>
    public class ModernTransportRealTests {
        private readonly ITestOutputHelper output;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModernTransportRealTests"/> class.
        /// </summary>
        /// <param name="output">The output helper used for diagnostic information.</param>
        public ModernTransportRealTests(ITestOutputHelper output) {
            this.output = output;
        }

        /// <summary>
        /// Ensures a live DNS over HTTP/3 query can complete against a known endpoint when explicitly enabled.
        /// </summary>
        [RealModernDnsTheory(DnsRequestFormat.DnsOverHttp3)]
        [InlineData(DnsEndpoint.Quad9Http3, "dns.quad9.net")]
        public async Task ShouldResolveAOverHttp3(DnsEndpoint endpoint, string expectedResolverHost) {
            DnsResponse response = await ExecuteWithRetriesAsync(endpoint, DnsRequestFormat.DnsOverHttp3);

            Assert.Equal(DnsResponseCode.NoError, response.Status);
            Assert.NotEmpty(response.Answers);
            Assert.Equal(DnsRequestFormat.DnsOverHttp3, response.Questions[0].RequestFormat);
            Assert.Contains(expectedResolverHost, response.ServerAddress ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures a live DNS over QUIC query can complete against a known endpoint when explicitly enabled.
        /// </summary>
        [RealModernDnsTheory(DnsRequestFormat.DnsOverQuic)]
        [InlineData(DnsEndpoint.Quad9Quic, "dns.quad9.net")]
        [InlineData(DnsEndpoint.CloudflareQuic, "1.1.1.1")]
        public async Task ShouldResolveAOverQuic(DnsEndpoint endpoint, string expectedResolverHost) {
            DnsResponse response = await ExecuteWithRetriesAsync(endpoint, DnsRequestFormat.DnsOverQuic);

            Assert.Equal(DnsResponseCode.NoError, response.Status);
            Assert.NotEmpty(response.Answers);
            Assert.Equal(DnsRequestFormat.DnsOverQuic, response.Questions[0].RequestFormat);
            Assert.Contains(expectedResolverHost, response.ServerAddress ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private async Task<DnsResponse> ExecuteWithRetriesAsync(DnsEndpoint endpoint, DnsRequestFormat expectedFormat) {
            string domain = "example.com";
            const int maxAttempts = 3;

            for (int attempt = 1; attempt <= maxAttempts; attempt++) {
                using var client = new ClientX(endpoint, timeOutMilliseconds: 4000) {
                    Debug = false
                };

                output.WriteLine($"Attempt {attempt}/{maxAttempts}: querying {domain} via {endpoint} ({expectedFormat})");
                DnsResponse response = await client.Resolve(domain, DnsRecordType.A, retryOnTransient: false);
                output.WriteLine($"  Status={response.Status}, Answers={response.Answers?.Length ?? 0}, Resolver={response.ServerAddress ?? "(unknown)"}, Error={response.Error ?? "none"}");

                if (response.Status == DnsResponseCode.NoError && response.Answers?.Length > 0) {
                    return response;
                }

                if (attempt < maxAttempts) {
                    await Task.Delay(TimeSpan.FromMilliseconds(300 * attempt));
                }
            }

            using var finalClient = new ClientX(endpoint, timeOutMilliseconds: 4000) {
                Debug = false
            };
            DnsResponse finalResponse = await finalClient.Resolve(domain, DnsRecordType.A, retryOnTransient: false);
            Assert.True(
                finalResponse.Status == DnsResponseCode.NoError && finalResponse.Answers?.Length > 0,
                $"Live {expectedFormat} check against {endpoint} failed. Status={finalResponse.Status}, Error={finalResponse.Error ?? "none"}");
            return finalResponse;
        }
    }
}
#endif
