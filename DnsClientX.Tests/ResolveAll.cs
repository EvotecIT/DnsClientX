namespace DnsClientX.Tests {
    public class ResolveFirst {
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
            var Client = new ClientX(endpoint);
            var answer = await Client.ResolveFirst("github.com", DnsRecordType.TXT);
            Assert.True(answer != null);
            Assert.True(answer.Value.Name == "github.com");
            Assert.True(answer.Value.Type == DnsRecordType.TXT);
            Assert.True(answer.Value.Data.Length > 0);
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
            var Client = new ClientX(endpoint);
            var answer = await Client.ResolveFirst("evotec.pl", DnsRecordType.A);
            Assert.True(answer != null);
            Assert.True(answer.Value.Name == "evotec.pl");
            Assert.True(answer.Value.Type == DnsRecordType.A);
        }
    }
}
