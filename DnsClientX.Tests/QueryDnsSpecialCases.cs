namespace DnsClientX.Tests {
    public class QueryDnsSpecialCases {
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
            Assert.True(response.Status != DnsResponseCode.NoError);
            foreach (DnsQuestion question in response.Questions) {
                Assert.True(question.Name == "spf-a.anotherexample.com");
                Assert.True(question.Type == DnsRecordType.A);
            }
        }
    }
}
