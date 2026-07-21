using System;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests the dependency-free telemetry adapter surface.
    /// </summary>
    public class DnsClientTelemetryTests {
        /// <summary>Publishes one terminal event for a successful public query.</summary>
        [Fact]
        public async Task PublishesQueryCompletion() {
            DnsQueryTelemetryEventArgs? observed = null;
            EventHandler<DnsQueryTelemetryEventArgs> handler = (_, args) => {
                if (args.Name == "telemetry.example") observed = args;
            };
            DnsClientTelemetry.QueryCompleted += handler;
            try {
                using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverUDP);
                client.ResolverOverride = (_, _, _) => Task.FromResult(new DnsResponse {
                    Status = DnsResponseCode.NoError,
                    Answers = Array.Empty<DnsAnswer>()
                });

                await client.Resolve("telemetry.example", retryOnTransient: false);

                Assert.NotNull(observed);
                Assert.Equal(DnsRecordType.A, observed!.Type);
                Assert.Equal(DnsResponseCode.NoError, observed.Status);
                Assert.Null(observed.Exception);
                Assert.True(observed.Succeeded);
                Assert.True(observed.Duration >= TimeSpan.Zero);
            } finally {
                DnsClientTelemetry.QueryCompleted -= handler;
            }
        }

        /// <summary>Subscriber failures never turn a completed DNS response into a query failure.</summary>
        [Fact]
        public async Task SubscriberExceptionIsIsolated() {
            EventHandler<DnsQueryTelemetryEventArgs> throwing = (_, _) => throw new InvalidOperationException("adapter failure");
            DnsClientTelemetry.QueryCompleted += throwing;
            try {
                using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverUDP);
                client.ResolverOverride = (_, _, _) => Task.FromResult(new DnsResponse {
                    Status = DnsResponseCode.NoError,
                    Answers = Array.Empty<DnsAnswer>()
                });

                DnsResponse response = await client.Resolve("isolated-telemetry.example", retryOnTransient: false);

                Assert.Equal(DnsResponseCode.NoError, response.Status);
            } finally {
                DnsClientTelemetry.QueryCompleted -= throwing;
            }
        }

        /// <summary>Terminal response errors are classified as failures and report the selected resolver.</summary>
        [Fact]
        public async Task ReportsActualResolverAndResponseError() {
            DnsQueryTelemetryEventArgs? observed = null;
            EventHandler<DnsQueryTelemetryEventArgs> handler = (_, args) => {
                if (args.Name == "telemetry-error.example") observed = args;
            };
            DnsClientTelemetry.QueryCompleted += handler;
            try {
                using var client = new ClientX("192.0.2.1", DnsRequestFormat.DnsOverUDP);
                client.ResolverOverride = (_, _, _) => {
                    var response = new DnsResponse { Status = DnsResponseCode.NoError, Error = "DNSSEC bogus" };
                    var selected = new Configuration("192.0.2.99", DnsRequestFormat.DnsOverTCP);
                    selected.SelectHostNameStrategy();
                    response.AddServerDetails(selected, Transport.Tcp);
                    return Task.FromResult(response);
                };

                await client.Resolve("telemetry-error.example", retryOnTransient: false);

                Assert.NotNull(observed);
                Assert.False(observed!.Succeeded);
                Assert.Equal("192.0.2.99", observed.Server);
                Assert.Equal(Transport.Tcp, observed.UsedTransport);
            } finally {
                DnsClientTelemetry.QueryCompleted -= handler;
            }
        }
    }
}
