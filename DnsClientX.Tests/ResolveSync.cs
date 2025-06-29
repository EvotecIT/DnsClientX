using DnsClientX;
using Xunit;

namespace DnsClientX.Tests {
    public class ResolveSync {
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
        public void ShouldWorkForTXTSync(DnsEndpoint endpoint) {
            var client = new ClientX(endpoint);
            var response = client.ResolveSync("github.com", DnsRecordType.TXT);
            foreach (DnsAnswer answer in response.Answers) {
                Assert.True(answer.Name == "github.com");
                Assert.True(answer.Type == DnsRecordType.TXT);
                Assert.True(answer.Data.Length > 0);
            }
        }

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
        public void ShouldWorkForASync(DnsEndpoint endpoint) {
            var client = new ClientX(endpoint);
            var response = client.ResolveSync("evotec.pl", DnsRecordType.A);
            foreach (DnsAnswer answer in response.Answers) {
                Assert.True(answer.Name == "evotec.pl");
                Assert.True(answer.Type == DnsRecordType.A);
                Assert.True(answer.Data.Length > 0);
            }
        }

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
        public void ShouldWorkForPTRSync(DnsEndpoint endpoint) {
            var client = new ClientX(endpoint);
            var response = client.ResolveSync("1.1.1.1", DnsRecordType.PTR);
            foreach (DnsAnswer answer in response.Answers) {
                Assert.True(answer.Data == "one.one.one.one");
                Assert.True(answer.Name == "1.1.1.1.in-addr.arpa");
                Assert.True(answer.Type == DnsRecordType.PTR);
                Assert.True(answer.Data.Length > 0);
            }
        }

        [Theory]
        [InlineData(DnsEndpoint.Cloudflare)]
        public void ShouldWorkForMultipleDomainsSync(DnsEndpoint endpoint) {
            var client = new ClientX(endpoint);
            var domains = new[] { "evotec.pl", "google.com" };
            var responses = client.ResolveSync(domains, DnsRecordType.A);
            foreach (var domain in domains) {
                var response = responses.First(r => r.Questions.Any(q => q.Name == domain));
                foreach (DnsAnswer answer in response.Answers) {
                    Assert.True(answer.Name == domain);
                    Assert.True(answer.Type == DnsRecordType.A);
                    Assert.True(answer.Data.Length > 0);
                }
            }
        }

        [Theory]
        [InlineData(DnsEndpoint.Cloudflare)]
        public void ShouldWorkForMultipleTypesSync(DnsEndpoint endpoint) {
            var client = new ClientX(endpoint);
            var types = new[] { DnsRecordType.A, DnsRecordType.TXT };
            var responses = client.ResolveSync("evotec.pl", types);
            foreach (var type in types) {
                var response = responses.First(r => r.Questions.Any(q => q.Type == type));
                foreach (DnsAnswer answer in response.Answers) {
                    Assert.True(answer.Name == "evotec.pl");
                    Assert.True(answer.Type == type);
                    Assert.True(answer.Data.Length > 0);
                }
            }
        }

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
        public void ShouldWorkForFirstSyncTXT(DnsEndpoint endpoint) {
            var client = new ClientX(endpoint);
            var answer = client.ResolveFirstSync("github.com", DnsRecordType.TXT);
            Assert.True(answer != null);
            Assert.True(answer.Value.Name == "github.com");
            Assert.True(answer.Value.Type == DnsRecordType.TXT);
            Assert.True(answer.Value.Data.Length > 0);
        }

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
        public void ShouldWorkForFirstSyncA(DnsEndpoint endpoint) {
            var client = new ClientX(endpoint);
            var answer = client.ResolveFirstSync("evotec.pl", DnsRecordType.A);
            Assert.True(answer != null);
            Assert.True(answer.Value.Name == "evotec.pl");
            Assert.True(answer.Value.Type == DnsRecordType.A);
            Assert.True(answer.Value.Data.Length > 0);
        }

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
        public void ShouldWorkForAllSyncTXT(DnsEndpoint endpoint) {
            var client = new ClientX(endpoint);
            var answers = client.ResolveAllSync("github.com", DnsRecordType.TXT);
            foreach (DnsAnswer answer in answers) {
                Assert.True(answer.Name == "github.com");
                Assert.True(answer.Type == DnsRecordType.TXT);
                Assert.True(answer.Data.Length > 0);
            }
        }

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
        public void ShouldWorkForAllSyncA(DnsEndpoint endpoint) {
            var client = new ClientX(endpoint);
            var answers = client.ResolveAllSync("evotec.pl", DnsRecordType.A);
            foreach (DnsAnswer answer in answers) {
                Assert.True(answer.Name == "evotec.pl");
                Assert.True(answer.Type == DnsRecordType.A);
            }
        }
    }
}
