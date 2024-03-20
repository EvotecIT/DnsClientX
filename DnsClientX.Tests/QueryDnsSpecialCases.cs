namespace DnsClientX.Tests {
    public class QueryDnsSpecialCases {
        [Theory]
        [InlineData(DnsEndpoint.Cloudflare)]
        [InlineData(DnsEndpoint.CloudflareFamily)]
        [InlineData(DnsEndpoint.CloudflareSecurity)]
        [InlineData(DnsEndpoint.CloudflareWireFormat)]
        [InlineData(DnsEndpoint.Google)]
        [InlineData(DnsEndpoint.Quad9)]
        [InlineData(DnsEndpoint.Quad9ECS)]
        [InlineData(DnsEndpoint.Quad9Unsecure)]
        [InlineData(DnsEndpoint.OpenDNS)]
        [InlineData(DnsEndpoint.OpenDNSFamily)]
        /// <summary>
        /// This test case is for a special case where the query is expected to fail.
        /// </summary>
        public async void ShouldDeliverResponseOnFailedQueries(DnsEndpoint endpoint) {
            var response = await ClientX.QueryDns("spf-a.anotherexample.com", DnsRecordType.A, endpoint);
            Assert.True(response.Questions.Length == 1);
            Assert.True(response.Answers.Length == 0);

            foreach (DnsQuestion question in response.Questions) {
                Assert.True(question.Name == "spf-a.anotherexample.com");
                Assert.True(question.Type == DnsRecordType.A);
            }

            Assert.True(response.Comments.Length > 0);
        }
    }
}
