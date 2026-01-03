namespace DnsClientX.Tests {
    /// <summary>
    /// Tests the <see cref="ClientX.ResolveFirst"/> helper across various endpoints.
    /// </summary>
    public class ResolveFirst {
        /// <summary>
        /// Resolves the first TXT record for the specified endpoint.
        /// </summary>
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
        public async Task ShouldWorkForTXT(DnsEndpoint endpoint) {
            if (TestSkipHelpers.ShouldSkipEndpoint(endpoint)) return;
            using var Client = new ClientX(endpoint);
            var answer = await Client.ResolveFirst("github.com", DnsRecordType.TXT, cancellationToken: CancellationToken.None);
            Assert.True(answer != null);
            Assert.True(answer.Value.Name == "github.com");
            Assert.True(answer.Value.Type == DnsRecordType.TXT);
            Assert.True(answer.Value.Data.Length > 0);
        }

        /// <summary>
        /// Resolves the first A record for the specified endpoint.
        /// </summary>
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
        public async Task ShouldWorkForA(DnsEndpoint endpoint) {
            if (TestSkipHelpers.ShouldSkipEndpoint(endpoint)) return;
            using var Client = new ClientX(endpoint);
            var answer = await Client.ResolveFirst("evotec.pl", DnsRecordType.A, cancellationToken: CancellationToken.None);
            Assert.True(answer != null);
            Assert.True(answer.Value.Type == DnsRecordType.A);
            Assert.True(!string.IsNullOrWhiteSpace(answer.Value.Name), "Expected answer name to not be empty");
            Assert.True(System.Net.IPAddress.TryParse(answer.Value.Data, out var ipAddress) && ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork, $"Expected A record data to be an IPv4 address but got '{answer.Value.Data}' (name '{answer.Value.Name}', original '{answer.Value.OriginalName}')");
        }

        /// <summary>
        /// Resolves the first TXT record synchronously for the specified endpoint.
        /// </summary>
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
        public void ShouldWorkForTXT_Sync(DnsEndpoint endpoint) {
            if (TestSkipHelpers.ShouldSkipEndpoint(endpoint)) return;
            using var Client = new ClientX(endpoint);
            var answer = Client.ResolveFirstSync("github.com", DnsRecordType.TXT, cancellationToken: CancellationToken.None);
            Assert.True(answer != null);
            Assert.True(answer.Value.Name == "github.com");
            Assert.True(answer.Value.Type == DnsRecordType.TXT);
            Assert.True(answer.Value.Data.Length > 0);
        }

        /// <summary>
        /// Resolves the first A record synchronously for the specified endpoint.
        /// </summary>
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
        public void ShouldWorkForA_Sync(DnsEndpoint endpoint) {
            if (TestSkipHelpers.ShouldSkipEndpoint(endpoint)) return;
            using var Client = new ClientX(endpoint);
            var answer = Client.ResolveFirstSync("evotec.pl", DnsRecordType.A, cancellationToken: CancellationToken.None);
            Assert.True(answer != null);
            Assert.True(answer.Value.Type == DnsRecordType.A);
            Assert.True(!string.IsNullOrWhiteSpace(answer.Value.Name), "Expected answer name to not be empty");
            Assert.True(System.Net.IPAddress.TryParse(answer.Value.Data, out var ipAddress) && ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork, $"Expected A record data to be an IPv4 address but got '{answer.Value.Data}' (name '{answer.Value.Name}', original '{answer.Value.OriginalName}')");
        }
    }
}
