namespace DnsClientX.Tests {
    public class ResolveAll {
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
        public async void ShouldWorkForTXT(DnsEndpoint endpoint) {
            var Client = new DnsClientX(endpoint);
            DnsAnswer[] aAnswers = await Client.ResolveAll("github.com", DnsRecordType.TXT);
            foreach (DnsAnswer answer in aAnswers) {
                Assert.True(answer.Name == "github.com");
                Assert.True((bool)(answer.Type == DnsRecordType.TXT));
                Assert.True(answer.Data.Length > 0);
            }
        }

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
        public async void ShouldWorkForA(DnsEndpoint endpoint) {
            var Client = new DnsClientX(endpoint);
            DnsAnswer[] aAnswers = await Client.ResolveAll("evotec.pl", DnsRecordType.A);
            foreach (DnsAnswer answer in aAnswers) {
                Assert.True(answer.Name == "evotec.pl");
                Assert.True((bool)(answer.Type == DnsRecordType.A));
            }
        }
    }
}
