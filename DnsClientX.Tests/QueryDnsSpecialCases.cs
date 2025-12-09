namespace DnsClientX.Tests {
    /// <summary>
    /// Tests scenarios where queries are expected to return errors while still producing a response.
    /// </summary>
    public class QueryDnsSpecialCases {
        /// <summary>
        /// Queries a domain known to fail and verifies the response is returned with an error status.
        /// </summary>
        /// <param name="endpoint">The endpoint used for the query.</param>
        [Theory]
        [InlineData(DnsEndpoint.System)]
        [InlineData(DnsEndpoint.SystemTcp)]
        [InlineData(DnsEndpoint.Cloudflare)]
        [InlineData(DnsEndpoint.CloudflareFamily)]
        [InlineData(DnsEndpoint.CloudflareSecurity)]
        [InlineData(DnsEndpoint.CloudflareWireFormat)]
        [InlineData(DnsEndpoint.CloudflareWireFormatPost)]
        [InlineData(DnsEndpoint.CloudflareOdoh)]
        [InlineData(DnsEndpoint.Google)]
        [InlineData(DnsEndpoint.GoogleWireFormat)]
        [InlineData(DnsEndpoint.GoogleWireFormatPost)]



        [InlineData(DnsEndpoint.OpenDNS)]
        [InlineData(DnsEndpoint.OpenDNSFamily)]
        public async Task ShouldDeliverResponseOnFailedQueries(DnsEndpoint endpoint) {
            using var client = new ClientX(endpoint);
            client.ResolverOverride = (name, type, ct) => Task.FromResult(new DnsResponse {
                Answers = Array.Empty<DnsAnswer>(),
                Status = DnsResponseCode.ServerFailure,
                Questions = new[] { new DnsQuestion { Name = name, Type = type } }
            });

            var response = await client.Resolve("spf-a.anotherexample.com", DnsRecordType.A, retryOnTransient: false);

            Assert.Empty(response.Answers);
            Assert.NotEqual(DnsResponseCode.NoError, response.Status);
            Assert.NotNull(response.Questions);
            Assert.Single(response.Questions);
            Assert.Equal("spf-a.anotherexample.com", response.Questions[0].Name);
            Assert.Equal(DnsRecordType.A, response.Questions[0].Type);
        }
    }
}
