namespace DnsClientX.Tests {
    public class QueryDnsSpecialCases {
        /// <summary>
        /// This test case is for a special case where the query is expected to fail.
        /// </summary>
        /// <param name="endpoint"></param>
        [Theory]
        [InlineData(DnsEndpoint.System)]
        [InlineData(DnsEndpoint.SystemTcp)]
        [InlineData(DnsEndpoint.Cloudflare)]
        [InlineData(DnsEndpoint.CloudflareFamily)]
        [InlineData(DnsEndpoint.CloudflareSecurity)]
        [InlineData(DnsEndpoint.CloudflareWireFormat)]
        [InlineData(DnsEndpoint.CloudflareWireFormatPost)]
        [InlineData(DnsEndpoint.Google)]
        [InlineData(DnsEndpoint.GoogleWireFormat)]
        [InlineData(DnsEndpoint.GoogleWireFormatPost)]



        [InlineData(DnsEndpoint.OpenDNS)]
        [InlineData(DnsEndpoint.OpenDNSFamily)]
        public async Task ShouldDeliverResponseOnFailedQueries(DnsEndpoint endpoint) {
            var response = await ClientX.QueryDns("spf-a.anotherexample.com", DnsRecordType.A, endpoint).ConfigureAwait(false);
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
