using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests verifying resolution starting at DNS root servers.
    /// </summary>
    public class ResolveFromRootTests {
        /// <summary>
        /// Queries the root servers directly to resolve an A record.
        /// </summary>
        [Fact(Skip = "External dependency - requires root servers")] // network unreachable in CI
        public async Task ShouldResolveARecordFromRoot() {
            var response = await ClientX.QueryDns("github.com", DnsRecordType.A, DnsEndpoint.RootServer);
            Assert.NotEmpty(response.Answers);
            foreach (var ans in response.Answers) {
                Assert.Equal(DnsRecordType.A, ans.Type);
            }
        }

        [Fact]
        /// <summary>
        /// Ensures root lookups created in parallel keep their clients alive until completion.
        /// </summary>
        public async Task QueryDns_MultipleRootLookups_DoesNotDisposeClientsEarly() {
            var createdClients = new List<TrackingClientX>();
            var completions = new List<TaskCompletionSource<DnsResponse>>();
            Func<ClientX> originalFactory = ClientX.RootClientFactory;
            var originalResolver = ClientX.RootResolveOverride;

            try {
                ClientX.RootClientFactory = () => {
                    var client = new TrackingClientX();
                    createdClients.Add(client);
                    return client;
                };

                ClientX.RootResolveOverride = (client, name, recordType, cancellationToken) => {
                    var tcs = new TaskCompletionSource<DnsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
                    completions.Add(tcs);
                    return tcs.Task;
                };

                Task<DnsResponse[]> queryTask = ClientX.QueryDns(new[] { "example.com", "example.net" }, DnsRecordType.A, DnsEndpoint.RootServer);

                await Task.Delay(10);
                Assert.Equal(2, completions.Count);
                Assert.All(createdClients, client => Assert.False(client.IsDisposed));

                foreach (var completion in completions) {
                    completion.SetResult(new DnsResponse {
                        Answers = new[] {
                            new DnsAnswer {
                                Name = "example.com",
                                Type = DnsRecordType.A,
                                TTL = 60,
                                DataRaw = "127.0.0.1"
                            }
                        }
                    });
                }

                var responses = await queryTask;
                Assert.Equal(2, responses.Length);
                Assert.All(createdClients, client => Assert.True(client.IsDisposed));
            } finally {
                ClientX.RootClientFactory = originalFactory;
                ClientX.RootResolveOverride = originalResolver;
            }
        }

        private sealed class TrackingClientX : ClientX {
            public bool IsDisposed { get; private set; }

            protected override void Dispose(bool disposing) {
                base.Dispose(disposing);
                IsDisposed = true;
            }

            protected override async ValueTask DisposeAsyncCore() {
                await base.DisposeAsyncCore().ConfigureAwait(false);
                IsDisposed = true;
            }
        }
    }
}
