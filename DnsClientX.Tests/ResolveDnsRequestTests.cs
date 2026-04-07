using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for the reusable request-based DNS query execution surface.
    /// </summary>
    [Collection("NoParallel")]
    public class ResolveDnsRequestTests {
        /// <summary>
        /// Verifies that the reusable request API expands patterns and executes the single-provider path.
        /// </summary>
        [Fact]
        public async Task QueryDns_RequestWithPatternAndProvider_UsesReusableExecutionPath() {
            try {
                var calls = new List<(DnsRecordType Type, string[] Names)>();

                ClientX.QueryDnsRequestOverride = (request, names, type, ct) => {
                    calls.Add((type, names.ToArray()));
                    return Task.FromResult(names.Select(name => new DnsResponse {
                        Questions = new[] {
                            new DnsQuestion {
                                Name = name,
                                OriginalName = name,
                                Type = type,
                                HostName = request.DnsProviders[0].ToString()
                            }
                        },
                        Answers = new[] {
                            new DnsAnswer {
                                Name = name,
                                Type = type,
                                TTL = 30,
                                DataRaw = "127.0.0.1"
                            }
                        },
                        Status = DnsResponseCode.NoError
                    }).ToArray());
                };

                var request = new ResolveDnsRequest {
                    Pattern = "host[1-2].example.com",
                    RecordTypes = new[] { DnsRecordType.A, DnsRecordType.MX },
                    DnsProviders = new[] { DnsEndpoint.Cloudflare }
                };

                var responses = await ClientX.QueryDns(request);

                Assert.Equal(4, responses.Length);
                Assert.Equal(2, calls.Count);
                Assert.Equal(new[] { "host1.example.com", "host2.example.com" }, calls[0].Names);
                Assert.Equal(DnsRecordType.A, calls[0].Type);
                Assert.Equal(DnsRecordType.MX, calls[1].Type);
                Assert.Contains(responses, r => r.Questions[0].Type == DnsRecordType.MX);
            } finally {
                ClientX.QueryDnsRequestOverride = null;
            }
        }

        /// <summary>
        /// Verifies that resolver endpoint input flows through the multi-resolver execution path.
        /// </summary>
        [Fact]
        public async Task QueryDns_RequestWithResolverEndpoints_UsesMultiResolverExecutionPath() {
            try {
                DnsMultiResolver.ResolveOverride = (ep, name, type, ct) => Task.FromResult(new DnsResponse {
                    Questions = new[] {
                        new DnsQuestion {
                            Name = name,
                            OriginalName = name,
                            Type = type,
                            HostName = ep.Host
                        }
                    },
                    Answers = new[] {
                        new DnsAnswer {
                            Name = name,
                            Type = type,
                            TTL = 30,
                            DataRaw = "127.0.0.1"
                        }
                    },
                    Status = DnsResponseCode.NoError
                });

                var request = new ResolveDnsRequest {
                    Names = new[] { "example.com" },
                    RecordTypes = new[] { DnsRecordType.A },
                    ResolverEndpoints = new[] { "1.1.1.1:53", "https://dns.google/dns-query" },
                    ResolverStrategy = MultiResolverStrategy.FirstSuccess
                };

                var responses = await ClientX.QueryDns(request);

                Assert.Single(responses);
                Assert.Equal(DnsResponseCode.NoError, responses[0].Status);
                Assert.Contains(responses[0].Questions[0].HostName, new[] { "1.1.1.1", "dns.google" });
            } finally {
                DnsMultiResolver.ResolveOverride = null;
            }
        }

        /// <summary>
        /// Verifies that multi-resolver execution honors retry settings for transient failures.
        /// </summary>
        [Fact]
        public async Task QueryDns_RequestWithResolverEndpoints_RetriesTransientFailures() {
            try {
                int calls = 0;
                DnsMultiResolver.ResolveOverride = (ep, name, type, ct) => {
                    calls++;
                    if (calls == 1) {
                        return Task.FromResult(new DnsResponse {
                            Questions = new[] {
                                new DnsQuestion {
                                    Name = name,
                                    OriginalName = name,
                                    Type = type,
                                    HostName = ep.Host
                                }
                            },
                            Status = DnsResponseCode.ServerFailure,
                            Error = "timeout",
                            ErrorCode = DnsQueryErrorCode.Timeout
                        });
                    }

                    return Task.FromResult(new DnsResponse {
                        Questions = new[] {
                            new DnsQuestion {
                                Name = name,
                                OriginalName = name,
                                Type = type,
                                HostName = ep.Host
                            }
                        },
                        Answers = new[] {
                            new DnsAnswer {
                                Name = name,
                                Type = type,
                                TTL = 30,
                                DataRaw = "127.0.0.1"
                            }
                        },
                        Status = DnsResponseCode.NoError
                    });
                };

                var request = new ResolveDnsRequest {
                    Names = new[] { "example.com" },
                    RecordTypes = new[] { DnsRecordType.A },
                    ResolverEndpoints = new[] { "1.1.1.1:53" },
                    RetryCount = 2,
                    RetryDelayMs = 0
                };

                var responses = await ClientX.QueryDns(request);

                Assert.Single(responses);
                Assert.Equal(DnsResponseCode.NoError, responses[0].Status);
                Assert.Equal(2, calls);
            } finally {
                DnsMultiResolver.ResolveOverride = null;
            }
        }

        /// <summary>
        /// Verifies that explicit server DoH requests preserve provider-specific JSON paths and port overrides.
        /// </summary>
        [Fact]
        public void CreateServerBaseUri_PreservesProviderSpecificJsonPathAndPort() {
            Uri google = ClientX.CreateServerBaseUri("dns.google", DnsRequestFormat.DnsOverHttpsJSON, 444);
            Assert.Equal("https://dns.google:444/resolve", google.AbsoluteUri);

            Uri cloudflare = ClientX.CreateServerBaseUri("1.1.1.1", DnsRequestFormat.DnsOverHttpsJSON);
            Assert.Equal("https://1.1.1.1/dns-query", cloudflare.AbsoluteUri);
        }

        /// <summary>
        /// Verifies that malformed explicit server values are rejected by the reusable request execution path.
        /// </summary>
        [Fact]
        public async Task QueryDns_RequestWithInvalidServer_ThrowsArgumentException() {
            var request = new ResolveDnsRequest {
                Names = new[] { "example.com" },
                RecordTypes = new[] { DnsRecordType.A },
                Servers = new[] { "bad host" }
            };

            var exception = await Assert.ThrowsAsync<ArgumentException>(() => ClientX.QueryDns(request));

            Assert.Contains("Malformed server address", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Verifies that only one resolver source can be specified on a reusable request.
        /// </summary>
        [Fact]
        public void ResolveDnsRequest_RejectsMultipleResolverSources() {
            var request = new ResolveDnsRequest {
                Names = new[] { "example.com" },
                RecordTypes = new[] { DnsRecordType.A },
                DnsProviders = new[] { DnsEndpoint.Cloudflare },
                Servers = new[] { "1.1.1.1" }
            };

            var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

            Assert.Contains("Specify only one resolver source", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
