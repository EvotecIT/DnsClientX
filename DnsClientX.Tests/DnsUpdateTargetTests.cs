using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>Prevents wire UPDATE operations from targeting implicit resolver profiles.</summary>
    [Collection("NoParallel")]
    public class DnsUpdateTargetTests {
        /// <summary>Built-in profiles cannot authorize a wire update, even before cancellation is observed.</summary>
        [Theory]
        [InlineData(DnsEndpoint.System, 0)]
        [InlineData(DnsEndpoint.System, 1)]
        [InlineData(DnsEndpoint.System, 2)]
        [InlineData(DnsEndpoint.SystemTcp, 0)]
        [InlineData(DnsEndpoint.SystemTcp, 1)]
        [InlineData(DnsEndpoint.SystemTcp, 2)]
        [InlineData(DnsEndpoint.RootServer, 0)]
        public async Task WireUpdate_RejectsBuiltInResolverProfile(DnsEndpoint endpoint, int operation) {
            SystemInformation.SetDnsServerProvider(() => new List<string> { "127.0.0.1" });
            try {
                using var client = new ClientX(endpoint);
                // Cancellation keeps the pre-fix reproduction from contacting any DNS server.
                var cancellationToken = new CancellationToken(canceled: true);

                NotSupportedException exception = await Assert.ThrowsAsync<NotSupportedException>(() =>
                    SendUpdateAsync(client, operation, cancellationToken));

                Assert.Contains("explicit authoritative", exception.Message, StringComparison.OrdinalIgnoreCase);
            } finally {
                SystemInformation.SetDnsServerProvider(null);
            }
        }

        /// <summary>Shared workflows preserve built-in profile provenance through both overloads.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task WireUpdateWorkflow_RejectsBuiltInResolverProfile(bool useSource) {
            SystemInformation.SetDnsServerProvider(() => new List<string> { "127.0.0.1" });
            try {
                var cancellationToken = new CancellationToken(canceled: true);
                NotSupportedException exception = await Assert.ThrowsAsync<NotSupportedException>(() => useSource
                    ? ResolverUpdateWorkflow.UpdateAsync(
                        new ResolverExecutionTargetSource { BuiltInEndpoints = new[] { DnsEndpoint.SystemTcp } },
                        "example.com", "www.example.com", DnsRecordType.A, "192.0.2.10", cancellationToken: cancellationToken)
                    : ResolverUpdateWorkflow.UpdateAsync(
                        new ResolverExecutionTarget { BuiltInEndpoint = DnsEndpoint.SystemTcp },
                        "example.com", "www.example.com", DnsRecordType.A, "192.0.2.10", cancellationToken: cancellationToken));

                Assert.Contains("explicit authoritative", exception.Message, StringComparison.OrdinalIgnoreCase);
            } finally {
                SystemInformation.SetDnsServerProvider(null);
            }
        }

        private static Task<DnsResponse> SendUpdateAsync(ClientX client, int operation, CancellationToken cancellationToken) {
            return operation switch {
                0 => client.UpdateRecordAsync("example.com", "www.example.com", DnsRecordType.A, "192.0.2.10", cancellationToken: cancellationToken),
                1 => client.DeleteRecordAsync("example.com", "www.example.com", DnsRecordType.A, cancellationToken),
                _ => client.DeleteRecordValueAsync("example.com", "www.example.com", DnsRecordType.A, "192.0.2.10", cancellationToken)
            };
        }
    }
}
