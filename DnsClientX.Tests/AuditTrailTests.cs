using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests related to the audit trail functionality of <see cref="ClientX"/>.
    /// </summary>
    public class AuditTrailTests {
        /// <summary>
        /// Ensures that no audit entries are created when auditing is disabled.
        /// </summary>
        [RealDnsFact]
        public async Task ShouldNotRecordWhenDisabled() {
            using var client = new ClientX();
            var response = await client.Resolve("example.com", DnsRecordType.A, retryOnTransient: false);
            Assert.Empty(client.AuditTrail);
        }

        /// <summary>
        /// Ensures that responses are recorded when auditing is enabled.
        /// </summary>
        [RealDnsFact]
        public async Task ShouldRecordResponseWhenEnabled() {
            using var client = new ClientX { EnableAudit = true };
            var response = await client.Resolve("example.com", DnsRecordType.A, retryOnTransient: false);
            Assert.Single(client.AuditTrail);
            var entry = Assert.Single(client.AuditTrail);
            Assert.Equal("example.com", entry.Name);
            Assert.Equal(DnsRecordType.A, entry.RecordType);
            Assert.Same(response, entry.Response);
            Assert.Null(entry.Exception);
            Assert.True(entry.Duration >= TimeSpan.Zero);
            Assert.Equal(client.EndpointConfiguration.SelectionStrategy, entry.SelectionStrategy);
            Assert.Equal(client.EndpointConfiguration.RequestFormat, entry.RequestFormat);
            Assert.False(entry.ServedFromCache);
        }

        private class ThrowingHandler : HttpMessageHandler {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
                throw new HttpRequestException("network error");
            }
        }

        private sealed class CountingHandler : HttpMessageHandler {
            private readonly string _json;

            public CountingHandler(string json) {
                _json = json;
            }

            public int Calls { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
                Calls++;
                var response = new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new StringContent(_json)
                };
                return Task.FromResult(response);
            }
        }

        private static void InjectClient(ClientX client, HttpClient httpClient) {
            httpClient.Timeout = TimeSpan.FromMilliseconds(client.EndpointConfiguration.TimeOut);
            var clientsField = typeof(ClientX).GetField("_clients", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var clients = (Dictionary<DnsSelectionStrategy, HttpClient>)clientsField.GetValue(client)!;
            clients[client.EndpointConfiguration.SelectionStrategy] = httpClient;
            var clientField = typeof(ClientX).GetField("Client", BindingFlags.NonPublic | BindingFlags.Instance)!;
            clientField.SetValue(client, httpClient);
        }

        /// <summary>
        /// Ensures that exceptions are captured in the audit trail.
        /// </summary>
        [Fact]
        public async Task ShouldRecordException() {
            var handler = new ThrowingHandler();
            using var client = new ClientX("1.1.1.1", DnsRequestFormat.DnsOverHttps) { EnableAudit = true };
            var customClient = new HttpClient(handler) {
                BaseAddress = client.EndpointConfiguration.BaseUri,
                Timeout = TimeSpan.FromMilliseconds(client.EndpointConfiguration.TimeOut)
            };
            InjectClient(client, customClient);

            var response = await client.Resolve("example.com", DnsRecordType.A, retryOnTransient: false);
            var entry = Assert.Single(client.AuditTrail);
            Assert.NotNull(entry.Exception);
            Assert.Same(response, entry.Response);
            Assert.Equal(DnsResponseCode.ServerFailure, response.Status);
        }

        /// <summary>
        /// Ensures transient retries create one audit entry per attempt.
        /// </summary>
        [Theory]
        [InlineData(2)]
        [InlineData(3)]
        public async Task ShouldRecordRetryAttemptsWhenEnabled(int attempts) {
            using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverUDP) { EnableAudit = true };
            int calls = 0;
            client.ResolverOverride = (name, type, ct) => {
                calls++;
                return Task.FromResult(new DnsResponse {
                    Questions = new[] { new DnsQuestion { Name = name, Type = type, OriginalName = name, RequestFormat = DnsRequestFormat.DnsOverUDP } },
                    Status = calls < attempts ? DnsResponseCode.ServerFailure : DnsResponseCode.NoError,
                    UsedTransport = Transport.Udp
                });
            };

            DateTimeOffset started = DateTimeOffset.UtcNow;
            DnsResponse response = await client.Resolve("example.com", DnsRecordType.A, retryOnTransient: true, maxRetries: attempts, retryDelayMs: 1);
            DateTimeOffset completed = DateTimeOffset.UtcNow;

            Assert.Equal(attempts, calls);
            Assert.Equal(DnsResponseCode.NoError, response.Status);
            Assert.Equal(attempts - 1, response.RetryCount);
            Assert.Equal(attempts, client.AuditTrail.Count);

            var entries = client.AuditTrail.ToArray();
            Assert.Equal(Enumerable.Range(1, attempts), entries.Select(entry => entry.AttemptNumber));
            Assert.All(entries.Take(attempts - 1), entry => {
                Assert.Equal(DnsResponseCode.ServerFailure, entry.Response?.Status);
                Assert.NotNull(entry.Exception);
                Assert.Contains("transient response", entry.RetryReason, StringComparison.OrdinalIgnoreCase);
            });
            Assert.Same(response, entries[attempts - 1].Response);
            Assert.Null(entries[attempts - 1].Exception);
            Assert.Null(entries[attempts - 1].RetryReason);
            Assert.All(entries, entry => {
                Assert.Equal("example.com", entry.Name);
                Assert.Equal(DnsRecordType.A, entry.RecordType);
                Assert.InRange(entry.StartedAtUtc, started, completed);
                Assert.True(entry.Duration >= TimeSpan.Zero);
                Assert.Equal(client.EndpointConfiguration.SelectionStrategy, entry.SelectionStrategy);
                Assert.Equal("127.0.0.1", entry.ResolverHost);
                Assert.Equal(53, entry.ResolverPort);
                Assert.Equal(DnsRequestFormat.DnsOverUDP, entry.RequestFormat);
                Assert.Equal(Transport.Udp, entry.UsedTransport);
                Assert.False(entry.ServedFromCache);
            });
        }

        /// <summary>
        /// Ensures cache hits are recorded as cache-served attempts in the audit trail.
        /// </summary>
        [Fact]
        public async Task ShouldRecordCacheHitsWhenEnabled() {
            string name = $"cache-{Guid.NewGuid():N}.example.com";
            string json = $"{{\"Status\":0,\"Answer\":[{{\"name\":\"{name}\",\"type\":1,\"TTL\":60,\"data\":\"1.1.1.1\"}}]}}";
            var handler = new CountingHandler(json);

            using var client = new ClientX(new Uri("https://audit-cache.test/dns-query"), DnsRequestFormat.DnsOverHttpsJSON, enableCache: true) {
                EnableAudit = true
            };
            var httpClient = new HttpClient(handler) { BaseAddress = client.EndpointConfiguration.BaseUri };
            InjectClient(client, httpClient);

            var first = await client.Resolve(name, DnsRecordType.A, retryOnTransient: false);
            var second = await client.Resolve(name, DnsRecordType.A, retryOnTransient: false);

            Assert.NotSame(first, second);
            Assert.Equal(first.Answers[0].DataRaw, second.Answers[0].DataRaw);
            Assert.Equal(1, handler.Calls);
            Assert.Equal(2, client.AuditTrail.Count);

            var entries = client.AuditTrail.ToArray();
            Assert.False(entries[0].ServedFromCache);
            Assert.True(entries[1].ServedFromCache);
            Assert.Equal(DnsResponseCode.NoError, entries[1].Response?.Status);
            Assert.Equal(Transport.Doh, entries[1].UsedTransport);
        }
    }
}
