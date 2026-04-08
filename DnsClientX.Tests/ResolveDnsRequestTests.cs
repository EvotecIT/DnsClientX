using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
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
        /// Verifies that resolver endpoint files also flow through the multi-resolver execution path.
        /// </summary>
        [Fact]
        public async Task QueryDns_RequestWithResolverEndpointFile_UsesMultiResolverExecutionPath() {
            string resolverFile = Path.GetTempFileName();
            File.WriteAllText(resolverFile, "udp@1.1.1.1:53\r\n");

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
                    ResolverEndpointFiles = new[] { resolverFile },
                    ResolverStrategy = MultiResolverStrategy.FirstSuccess
                };

                var responses = await ClientX.QueryDns(request);

                Assert.Single(responses);
                Assert.Equal(DnsResponseCode.NoError, responses[0].Status);
                Assert.Equal("1.1.1.1", responses[0].Questions[0].HostName);
            } finally {
                DnsMultiResolver.ResolveOverride = null;
                File.Delete(resolverFile);
            }
        }

        /// <summary>
        /// Verifies that a reusable request can load a recommended resolver from a saved selection snapshot.
        /// </summary>
        [Fact]
        public async Task QueryDns_RequestWithResolverSelectionPath_UsesRecommendedResolver() {
            string snapshotPath = Path.GetTempFileName();

            try {
                ResolverScoreStore.Save(snapshotPath, new ResolverScoreSnapshot {
                    Summary = new ResolverScoreSummary {
                        Mode = ResolverScoreMode.Benchmark,
                        RecommendationAvailable = true,
                        RecommendedTarget = "Cloudflare",
                        RecommendedResolver = "1.1.1.1:53",
                        RecommendedTransport = "Doh",
                        RecommendedAverageMs = 7
                    }
                });

                ClientX.QueryDnsRequestOverride = (request, names, type, ct) => Task.FromResult(names.Select(name => new DnsResponse {
                    Questions = new[] {
                        new DnsQuestion {
                            Name = name,
                            OriginalName = name,
                            Type = type,
                            HostName = "Cloudflare"
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

                var request = new ResolveDnsRequest {
                    Names = new[] { "example.com" },
                    RecordTypes = new[] { DnsRecordType.A },
                    ResolverSelectionPath = snapshotPath
                };

                var responses = await ClientX.QueryDns(request);

                Assert.Single(responses);
                Assert.Equal("Cloudflare", responses[0].Questions[0].HostName);
            } finally {
                ClientX.QueryDnsRequestOverride = null;
                File.Delete(snapshotPath);
            }
        }

        /// <summary>
        /// Verifies that an explicit resolver selection uses the single-query execution path instead of the multi-resolver flow.
        /// </summary>
        [Fact]
        public async Task QueryDns_RequestWithExplicitResolverSelection_UsesSingleTargetExecutionPath() {
            string snapshotPath = Path.GetTempFileName();

            try {
                ResolverScoreStore.Save(snapshotPath, new ResolverScoreSnapshot {
                    Summary = new ResolverScoreSummary {
                        Mode = ResolverScoreMode.Probe,
                        RecommendationAvailable = true,
                        RecommendedTarget = "udp@127.0.0.1:53",
                        RecommendedResolver = "127.0.0.1:53",
                        RecommendedTransport = "Udp",
                        RecommendedAverageMs = 5
                    }
                });

                DnsMultiResolver.ResolveOverride = (_, _, _, _) => throw new InvalidOperationException("Multi-resolver path should not be used.");
                ClientX.QueryDnsRequestOverride = (request, names, type, ct) => {
                    Assert.Empty(request.DnsProviders);
                    Assert.Empty(request.ResolverEndpoints);
                    return Task.FromResult(names.Select(name => new DnsResponse {
                        Questions = new[] {
                            new DnsQuestion {
                                Name = name,
                                OriginalName = name,
                                Type = type,
                                HostName = "127.0.0.1"
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
                    Names = new[] { "example.com" },
                    RecordTypes = new[] { DnsRecordType.A },
                    ResolverSelectionPath = snapshotPath
                };

                DnsResponse[] responses = await ClientX.QueryDns(request);

                Assert.Single(responses);
                Assert.Equal("127.0.0.1", responses[0].Questions[0].HostName);
            } finally {
                DnsMultiResolver.ResolveOverride = null;
                ClientX.QueryDnsRequestOverride = null;
                File.Delete(snapshotPath);
            }
        }

        /// <summary>
        /// Verifies that request-level HTTP and fallback options are preserved when a selected resolver resolves to an explicit endpoint.
        /// </summary>
        [Fact]
        public void CreateClientForEndpoint_RequestOptionsArePreserved() {
            MethodInfo method = typeof(ClientX)
                .GetMethod("CreateClientForEndpoint", BindingFlags.NonPublic | BindingFlags.Static)!;

            var request = new ResolveDnsRequest {
                Names = new[] { "example.com" },
                RecordTypes = new[] { DnsRecordType.A },
                TimeOutMilliseconds = 4321,
                UserAgent = "DnsClientX.Tests",
                HttpVersion = new Version(3, 0),
                IgnoreCertificateErrors = true,
                UseTcpFallback = false,
                ProxyUri = new Uri("http://127.0.0.1:8888"),
                MaxConnectionsPerServer = 12
            };
            var endpoint = new DnsResolverEndpoint {
                Transport = Transport.Doh,
                DohUrl = new Uri("https://dns.example/dns-query"),
                AllowTcpFallback = true
            };

            using var client = (ClientX)method.Invoke(null, new object?[] { request, endpoint, null })!;

            Assert.Equal("DnsClientX.Tests", client.EndpointConfiguration.UserAgent);
            Assert.Equal(new Version(3, 0), client.EndpointConfiguration.HttpVersion);
            Assert.True(client.IgnoreCertificateErrors);
            Assert.False(client.EndpointConfiguration.UseTcpFallback);
            Assert.Equal(12, client.EndpointConfiguration.MaxConnectionsPerServer);
            Assert.Equal(4321, client.EndpointConfiguration.TimeOut);
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

        /// <summary>
        /// Verifies that EDNS buffer size is constrained to the wire-level 16-bit payload limit.
        /// </summary>
        [Fact]
        public void ResolveDnsRequest_RejectsEdnsBufferSizeAboveUInt16() {
            var request = new ResolveDnsRequest {
                Names = new[] { "example.com" },
                RecordTypes = new[] { DnsRecordType.A },
                DnsProviders = new[] { DnsEndpoint.Cloudflare },
                EdnsBufferSize = ushort.MaxValue + 1
            };

            var exception = Assert.Throws<ArgumentOutOfRangeException>(() => request.Validate());

            Assert.Contains("EdnsBufferSize", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Verifies that malformed ECS input is rejected before request execution.
        /// </summary>
        [Fact]
        public void ResolveDnsRequest_RejectsInvalidClientSubnet() {
            var request = new ResolveDnsRequest {
                Names = new[] { "example.com" },
                RecordTypes = new[] { DnsRecordType.A },
                DnsProviders = new[] { DnsEndpoint.Cloudflare },
                ClientSubnet = "192.0.2.1/99"
            };

            var exception = Assert.Throws<ArgumentException>(() => request.Validate());

            Assert.Contains("ClientSubnet", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Verifies that server-only transport overrides are rejected when no explicit server is configured.
        /// </summary>
        [Fact]
        public void ResolveDnsRequest_RejectsServerTransportOptionsWithoutServer() {
            var request = new ResolveDnsRequest {
                Names = new[] { "example.com" },
                RecordTypes = new[] { DnsRecordType.A },
                DnsProviders = new[] { DnsEndpoint.Cloudflare },
                RequestFormat = DnsRequestFormat.DnsOverHttps,
                Port = 443
            };

            var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

            Assert.Contains("requires at least one server", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Verifies that server fallback controls are rejected when no explicit server is configured.
        /// </summary>
        [Fact]
        public void ResolveDnsRequest_RejectsServerFlowFlagsWithoutServer() {
            var request = new ResolveDnsRequest {
                Names = new[] { "example.com" },
                RecordTypes = new[] { DnsRecordType.A },
                DnsProviders = new[] { DnsEndpoint.Cloudflare },
                Fallback = true
            };

            var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

            Assert.Contains("Fallback requires at least one server", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Verifies that valid IPv6 ECS input is accepted by reusable request validation.
        /// </summary>
        [Fact]
        public void ResolveDnsRequest_AllowsValidIpv6ClientSubnet() {
            var request = new ResolveDnsRequest {
                Names = new[] { "example.com" },
                RecordTypes = new[] { DnsRecordType.AAAA },
                DnsProviders = new[] { DnsEndpoint.Cloudflare },
                ClientSubnet = "2001:db8::/56"
            };

            request.Validate();

            EdnsOptions? options = request.CreateEdnsOptions();
            Assert.NotNull(options);
            Assert.Equal(new EdnsClientSubnetOption("2001:db8::/56"), options!.Subnet);
        }

        /// <summary>
        /// Verifies that richer EDNS request properties flow into reusable request option creation.
        /// </summary>
        [Fact]
        public void ResolveDnsRequest_CreateEdnsOptions_IncludesPaddingCookieAndNsid() {
            byte[] cookie = { 1, 2, 3, 4, 5, 6, 7, 8 };
            var request = new ResolveDnsRequest {
                Names = new[] { "example.com" },
                RecordTypes = new[] { DnsRecordType.A },
                DnsProviders = new[] { DnsEndpoint.Cloudflare },
                EdnsPaddingLength = 16,
                EdnsCookie = cookie,
                RequestNsid = true
            };

            request.Validate();

            EdnsOptions? options = request.CreateEdnsOptions();

            Assert.NotNull(options);
            Assert.Equal(16, options!.PaddingLength);
            Assert.Equal(cookie, options.Cookie);
            Assert.Contains(options.Options, option => option is NsidOption);
        }

        /// <summary>
        /// Verifies that malformed EDNS cookie lengths are rejected before request execution.
        /// </summary>
        [Fact]
        public void ResolveDnsRequest_RejectsInvalidEdnsCookieLength() {
            var request = new ResolveDnsRequest {
                Names = new[] { "example.com" },
                RecordTypes = new[] { DnsRecordType.A },
                DnsProviders = new[] { DnsEndpoint.Cloudflare },
                EdnsCookie = new byte[] { 1, 2, 3 }
            };

            var exception = Assert.Throws<ArgumentException>(() => request.Validate());

            Assert.Contains("EdnsCookie", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
