using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for optional max concurrency hints on array-based resolution helpers.
    /// </summary>
    public class MaxConcurrencyHintTests {
        /// <summary>
        /// Caps concurrency to 1 and preserves order for Resolve(name[]).
        /// </summary>
        [Fact]
        public async Task Resolve_Array_CapsConcurrency_And_PreservesOrder() {
            using var client = new ClientX(DnsEndpoint.System);
            client.EndpointConfiguration.MaxConcurrency = 1;

            int inFlight = 0;
            int maxInFlight = 0;
            object gate = new object();

            client.ResolverOverride = async (name, type, ct) => {
                int now;
                lock (gate) {
                    now = ++inFlight;
                    if (now > maxInFlight) maxInFlight = now;
                }
                try {
                    await Task.Delay(50, ct);
                } finally {
                    lock (gate) { inFlight--; }
                }

                return new DnsResponse {
                    Questions = new[] { new DnsQuestion { Name = name, Type = type, OriginalName = name } },
                    Answers = new[] { new DnsAnswer { Name = name, Type = type, DataRaw = "127.0.0.1" } },
                    Status = DnsResponseCode.NoError
                };
            };

            var names = Enumerable.Range(0, 5).Select(i => $"n{i}.example").ToArray();
            var responses = await client.Resolve(names, DnsRecordType.A);

            Assert.Equal(names.Length, responses.Length);
            Assert.True(maxInFlight <= 1, $"Expected maxInFlight <= 1, got {maxInFlight}");
            for (int i = 0; i < names.Length; i++) {
                Assert.NotNull(responses[i]);
                Assert.NotEmpty(responses[i].Questions);
                Assert.Equal(names[i].TrimEnd('.'), responses[i].Questions[0].Name);
            }
        }

        /// <summary>
        /// Caps concurrency to 2 and preserves order for ResolveFilter(name[]).
        /// </summary>
        [Fact]
        public async Task ResolveFilter_Array_CapsConcurrency_And_PreservesOrder() {
            using var client = new ClientX(DnsEndpoint.System);
            client.EndpointConfiguration.MaxConcurrency = 2;

            int inFlight = 0;
            int maxInFlight = 0;
            object gate = new object();

            client.ResolverOverride = async (name, type, ct) => {
                int now;
                lock (gate) {
                    now = ++inFlight;
                    if (now > maxInFlight) maxInFlight = now;
                }
                try {
                    await Task.Delay(50, ct);
                } finally {
                    lock (gate) { inFlight--; }
                }

                // Ensure TXT data contains the filter string
                return new DnsResponse {
                    Questions = new[] { new DnsQuestion { Name = name, Type = type, OriginalName = name } },
                    Answers = new[] { new DnsAnswer { Name = name, Type = DnsRecordType.TXT, DataRaw = "\"match\"" } },
                    Status = DnsResponseCode.NoError
                };
            };

            var names = Enumerable.Range(0, 6).Select(i => $"x{i}.example").ToArray();
            var responses = await client.ResolveFilter(names, DnsRecordType.TXT, "match");

            // All should match and be returned, in the same input order
            Assert.Equal(names.Length, responses.Length);
            Assert.True(maxInFlight <= 2, $"Expected maxInFlight <= 2, got {maxInFlight}");
            for (int i = 0; i < names.Length; i++) {
                Assert.NotNull(responses[i]);
                Assert.NotEmpty(responses[i].Questions);
                Assert.Equal(names[i].TrimEnd('.'), responses[i].Questions[0].Name);
                Assert.NotEmpty(responses[i].Answers);
                Assert.Contains("match", responses[i].Answers[0].Data, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}

