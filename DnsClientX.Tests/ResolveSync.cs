using DnsClientX;
using System.Runtime.InteropServices;
using Xunit;
using Xunit.Abstractions;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests synchronous resolution APIs of <see cref="ClientX"/>.
    /// </summary>
    public class ResolveSync {
        private readonly ITestOutputHelper _output;

        /// <summary>
        /// Initializes a new instance of the test class.
        /// </summary>
        /// <param name="output">XUnit output helper.</param>
        public ResolveSync(ITestOutputHelper output)
        {
            _output = output;
        }

        private void LogDiagnostics(string message)
        {
            _output.WriteLine($"[Diagnostic] {message}");
        }

        private async Task<DnsResponse> TryResolveWithDiagnostics(ClientX client, string domain, DnsRecordType recordType, int maxRetries = 3)
        {
            LogDiagnostics($"Attempting to resolve {domain} for record type {recordType}");
            LogDiagnostics($"Using DNS configuration: {client.EndpointConfiguration.RequestFormat}");
            LogDiagnostics($"DNS Servers: {string.Join(", ", SystemInformation.GetDnsFromActiveNetworkCard())}");

            DnsResponse response = new DnsResponse();
            Exception? lastException = null;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    LogDiagnostics($"Attempt {attempt} of {maxRetries}");
                    response = client.ResolveSync(domain, recordType);

                    LogDiagnostics($"Response Status: {response.Status}");
                    LogDiagnostics($"Answer Count: {response.Answers?.Length ?? 0}");
                    if (response.Error != null)
                    {
                        LogDiagnostics($"Response Error: {response.Error}");
                    }

                    if (response.Status == DnsResponseCode.NoError && response.Answers?.Length > 0)
                    {
                        LogDiagnostics("Query successful");
                        return response;
                    }

                    LogDiagnostics($"Query failed with status {response.Status}");
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    LogDiagnostics($"Attempt {attempt} failed with exception: {ex.GetType().Name} - {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        LogDiagnostics($"Inner Exception: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
                    }
                }

                if (attempt < maxRetries)
                {
                    await Task.Delay(1000 * attempt); // Exponential backoff
                }
            }

            if (lastException != null)
            {
                throw new Exception($"DNS resolution failed after {maxRetries} attempts", lastException);
            }

            return response;
        }

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
        /// <summary>
        /// Performs synchronous TXT resolution for the specified endpoint.
        /// </summary>
        public async Task ShouldWorkForTXTSync(DnsEndpoint endpoint)
        {
            using var client = new ClientX(endpoint);
            var response = await TryResolveWithDiagnostics(client, "github.com", DnsRecordType.TXT);

            Assert.True(response.Answers.Length > 0, "Expected at least one answer");
            foreach (DnsAnswer answer in response.Answers)
            {
                Assert.True(answer.Name == "github.com", $"Expected answer name to be github.com but got {answer.Name}");
                Assert.True(answer.Type == DnsRecordType.TXT, $"Expected answer type to be TXT but got {answer.Type}");
                Assert.True(answer.Data.Length > 0, "Expected answer data to not be empty");
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
        [InlineData(DnsEndpoint.CloudflareOdoh)]
        [InlineData(DnsEndpoint.Google)]
        [InlineData(DnsEndpoint.GoogleWireFormat)]
        [InlineData(DnsEndpoint.GoogleWireFormatPost)]
        [InlineData(DnsEndpoint.OpenDNS)]
[InlineData(DnsEndpoint.OpenDNSFamily)]
        /// <summary>
        /// Performs synchronous TXT resolution returning only the first record.
        /// </summary>
        public async Task ShouldWorkForFirstSyncTXT(DnsEndpoint endpoint)
        {
            using var client = new ClientX(endpoint);
            var response = await TryResolveWithDiagnostics(client, "github.com", DnsRecordType.TXT);

            Assert.True(response.Answers.Length > 0, "Expected at least one answer");
            var answer = response.Answers[0];
            Assert.True(answer.Name == "github.com", $"Expected answer name to be github.com but got {answer.Name}");
            Assert.True(answer.Type == DnsRecordType.TXT, $"Expected answer type to be TXT but got {answer.Type}");
            Assert.True(answer.Data.Length > 0, "Expected answer data to not be empty");
        }

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
        /// <summary>
        /// Performs synchronous TXT resolution returning all records.
        /// </summary>
        public async Task ShouldWorkForAllSyncTXT(DnsEndpoint endpoint)
        {
            using var client = new ClientX(endpoint);
            var response = await TryResolveWithDiagnostics(client, "github.com", DnsRecordType.TXT);

            Assert.True(response.Answers.Length > 0, "Expected at least one answer");
            foreach (DnsAnswer answer in response.Answers)
            {
                Assert.True(answer.Name == "github.com", $"Expected answer name to be github.com but got {answer.Name}");
                Assert.True(answer.Type == DnsRecordType.TXT, $"Expected answer type to be TXT but got {answer.Type}");
                Assert.True(answer.Data.Length > 0, "Expected answer data to not be empty");
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
        [InlineData(DnsEndpoint.CloudflareOdoh)]
        [InlineData(DnsEndpoint.Google)]
        [InlineData(DnsEndpoint.GoogleWireFormat)]
        [InlineData(DnsEndpoint.GoogleWireFormatPost)]
        [InlineData(DnsEndpoint.OpenDNS)]
[InlineData(DnsEndpoint.OpenDNSFamily)]
        /// <summary>
        /// Performs asynchronous A record resolution for the specified endpoint.
        /// </summary>
        public async Task ShouldWorkForASync(DnsEndpoint endpoint)
        {
            using var client = new ClientX(endpoint);
            var response = await TryResolveWithDiagnostics(client, "evotec.pl", DnsRecordType.A);

            Assert.True(response.Answers.Length > 0, "Expected at least one answer");
            foreach (DnsAnswer answer in response.Answers)
            {
                Assert.True(answer.Name == "evotec.pl", $"Expected answer name to be evotec.pl but got {answer.Name}");
                Assert.True(answer.Type == DnsRecordType.A, $"Expected answer type to be A but got {answer.Type}");
                Assert.True(answer.Data.Length > 0, "Expected answer data to not be empty");
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
        [InlineData(DnsEndpoint.CloudflareOdoh)]
        [InlineData(DnsEndpoint.Google)]
        [InlineData(DnsEndpoint.GoogleWireFormat)]
        [InlineData(DnsEndpoint.GoogleWireFormatPost)]
        [InlineData(DnsEndpoint.OpenDNS)]
        [InlineData(DnsEndpoint.OpenDNSFamily)]
        /// <summary>
        /// Synchronously resolves PTR records for reverse lookups.
        /// </summary>
        public void ShouldWorkForPTRSync(DnsEndpoint endpoint) {
            using var client = new ClientX(endpoint);
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
        /// <summary>
        /// Resolves multiple domains synchronously for the given endpoint.
        /// </summary>
        public void ShouldWorkForMultipleDomainsSync(DnsEndpoint endpoint) {
            using var client = new ClientX(endpoint);
            var domains = new[] { "evotec.pl", "google.com" };
            var responses = client.ResolveSync(domains, DnsRecordType.A);
            foreach (var domain in domains) {
                var response = responses.First(r => r.Questions?.Any(q => q.Name == domain) == true);
                foreach (DnsAnswer answer in response.Answers) {
                    Assert.True(answer.Name == domain);
                    Assert.True(answer.Type == DnsRecordType.A);
                    Assert.True(answer.Data.Length > 0);
                }
            }
        }

        [Theory]
        [InlineData(DnsEndpoint.Cloudflare)]
        /// <summary>
        /// Resolves multiple record types synchronously for a single domain.
        /// </summary>
        public void ShouldWorkForMultipleTypesSync(DnsEndpoint endpoint) {
            using var client = new ClientX(endpoint);
            var types = new[] { DnsRecordType.A, DnsRecordType.TXT };
            var responses = client.ResolveSync("evotec.pl", types);
            foreach (var type in types) {
                var response = responses.First(r => r.Questions?.Any(q => q.Type == type) == true);
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
        [InlineData(DnsEndpoint.CloudflareOdoh)]
        [InlineData(DnsEndpoint.Google)]
        [InlineData(DnsEndpoint.GoogleWireFormat)]
        [InlineData(DnsEndpoint.GoogleWireFormatPost)]
        [InlineData(DnsEndpoint.OpenDNS)]
        [InlineData(DnsEndpoint.OpenDNSFamily)]
        /// <summary>
        /// Resolves the first A record synchronously for the given endpoint.
        /// </summary>
        public void ShouldWorkForFirstSyncA(DnsEndpoint endpoint) {
            using var client = new ClientX(endpoint);
            var answer = client.ResolveFirstSync("evotec.pl", DnsRecordType.A, cancellationToken: CancellationToken.None);
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
        [InlineData(DnsEndpoint.CloudflareOdoh)]
        [InlineData(DnsEndpoint.Google)]
        [InlineData(DnsEndpoint.GoogleWireFormat)]
        [InlineData(DnsEndpoint.GoogleWireFormatPost)]
        [InlineData(DnsEndpoint.OpenDNS)]
        [InlineData(DnsEndpoint.OpenDNSFamily)]
        /// <summary>
        /// Resolves all A records synchronously for the given endpoint.
        /// </summary>
        public void ShouldWorkForAllSyncA(DnsEndpoint endpoint) {
            using var client = new ClientX(endpoint);
            var answers = client.ResolveAllSync("evotec.pl", DnsRecordType.A);
            foreach (DnsAnswer answer in answers) {
                Assert.True(answer.Name == "evotec.pl");
                Assert.True(answer.Type == DnsRecordType.A);
            }
        }
    }
}
