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
            var response = await ClientX.QueryDns("spf-a.anotherexample.com", DnsRecordType.A, endpoint);
            Assert.True(response.Answers.Length == 0);
            Assert.True(response.Status != DnsResponseCode.NoError);
            Assert.NotNull(response.Questions);
            if (response.Questions.Length > 0) {
                Assert.True(response.Questions.Length == 1);
                foreach (DnsQuestion question in response.Questions) {
                    Assert.True(question.Name == "spf-a.anotherexample.com");
                    Assert.True(question.Type == DnsRecordType.A);
                }
            }
        }
    }
}
