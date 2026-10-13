using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>Protects explicit-server fallback, failure diagnostics, retry outcomes, and cancellation.</summary>
    [Collection("NoParallel")]
    public sealed class ResolveDnsServerFallbackTests {
        /// <summary>Exhausting one server's transport deadline must still allow fallback and all-server execution.</summary>
        [Theory]
        [InlineData(false, false, "timeout")]
        [InlineData(false, false, "task-canceled")]
        [InlineData(false, false, "operation-canceled")]
        [InlineData(false, true, "timeout")]
        [InlineData(false, true, "task-canceled")]
        [InlineData(false, true, "operation-canceled")]
        [InlineData(true, false, "timeout")]
        [InlineData(true, false, "task-canceled")]
        [InlineData(true, false, "operation-canceled")]
        [InlineData(true, true, "timeout")]
        [InlineData(true, true, "task-canceled")]
        [InlineData(true, true, "operation-canceled")]
        public async Task QueryDns_ExhaustedServerDeadlineContinuesToNextServer(bool streamed, bool allServers, string failureKind) {
            int calls = 0;
            try {
                ClientX.QueryDnsRequestOverride = (request, names, type, token) => {
                    if (++calls <= 2) {
                        Exception failure = failureKind switch {
                            "task-canceled" => new TaskCanceledException("First server deadline elapsed."),
                            "operation-canceled" => new OperationCanceledException("First server deadline elapsed."),
                            _ => new TimeoutException("First server deadline elapsed.")
                        };
                        throw failure;
                    }
                    return Task.FromResult(new[] { ServerTestResponse(names[0], type, DnsResponseCode.NoError) });
                };
                var request = ServerTestRequest(allServers);
                request.RetryCount = 2;

                DnsResponse[] responses = await ExecuteServerTestRequest(request, streamed);

                Assert.Equal(3, calls);
                Assert.Equal(2, responses.Length);
                Assert.Equal(DnsResponseCode.ServerFailure, responses[0].Status);
                Assert.Equal(DnsQueryErrorCode.Timeout, responses[0].ErrorCode);
                Assert.Equal(failureKind switch {
                    "task-canceled" => typeof(TaskCanceledException),
                    "operation-canceled" => typeof(OperationCanceledException),
                    _ => typeof(TimeoutException)
                }, responses[0].Exception!.GetType());
                Assert.Equal(DnsResponseCode.NoError, responses[1].Status);
            } finally { ClientX.QueryDnsRequestOverride = null; }
        }

        /// <summary>When all servers fail, each requested name/type retains a typed failure and endpoint diagnostic.</summary>
        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task QueryDns_AllServerDeadlinesReturnPerQueryDiagnostics(bool streamed, bool allServers) {
            try {
                ClientX.QueryDnsRequestOverride = (_, _, _, _) => throw new TimeoutException("Resolver deadline elapsed.");
                var request = ServerTestRequest(allServers);
                request.Names = new[] { "one.example", "two.example" };
                request.RecordTypes = new[] { DnsRecordType.A, DnsRecordType.AAAA };

                DnsResponse[] responses = await ExecuteServerTestRequest(request, streamed);

                Assert.Equal(8, responses.Length);
                Assert.All(responses, response => {
                    Assert.Equal(DnsResponseCode.ServerFailure, response.Status);
                    Assert.Equal(DnsQueryErrorCode.Timeout, response.ErrorCode);
                    Assert.IsType<TimeoutException>(response.Exception);
                    Assert.Contains("Resolver deadline elapsed", response.Error, StringComparison.Ordinal);
                    DnsQuestion question = Assert.Single(response.Questions);
                    Assert.Equal(DnsRequestFormat.DnsOverHttps, question.RequestFormat);
                    Assert.Contains(question.HostName, request.Servers);
                    Assert.Contains(question.Name, request.Names);
                    Assert.Contains(question.Type, request.RecordTypes);
                });
                foreach (string server in request.Servers)
                    foreach (string name in request.Names)
                        foreach (DnsRecordType type in request.RecordTypes)
                            Assert.Single(responses, response => response.Questions[0].HostName == server
                                && response.Questions[0].Name == name && response.Questions[0].Type == type);
            } finally { ClientX.QueryDnsRequestOverride = null; }
        }

        /// <summary>One failed name must not erase another name's successful result from the same server.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task QueryDns_MixedServerBatchPreservesEachQueryOutcome(bool streamed) {
            int completedPairs = 0;
            try {
                ClientX.QueryDnsRequestOverride = (request, names, type, token) => {
                    bool firstServer = completedPairs < request.Names.Length;
                    completedPairs += names.Length;
                    if (firstServer && names.Contains("timeout.example")) throw new TimeoutException("One name timed out.");
                    DnsResponse[] responses = names.Select(name => ServerTestResponse(name, type, DnsResponseCode.NoError)).ToArray();
                    foreach (DnsResponse response in responses) response.Questions[0].HostName = request.Servers[firstServer ? 0 : 1];
                    return Task.FromResult(responses);
                };
                var request = ServerTestRequest(allServers: true);
                request.Names = new[] { "success.example", "timeout.example" };

                DnsResponse[] responses = await ExecuteServerTestRequest(request, streamed);

                Assert.Equal(4, responses.Length);
                Assert.Equal(new[] { "success.example", "timeout.example", "success.example", "timeout.example" },
                    responses.Select(response => response.Questions[0].Name));
                Assert.Equal(DnsResponseCode.NoError, responses[0].Status);
                Assert.Equal(request.Servers[0], responses[0].Questions[0].HostName);
                Assert.Equal(DnsResponseCode.ServerFailure, responses[1].Status);
                Assert.Equal(DnsQueryErrorCode.Timeout, responses[1].ErrorCode);
                Assert.IsType<TimeoutException>(responses[1].Exception);
                Assert.All(responses.Skip(2), response => Assert.Equal(DnsResponseCode.NoError, response.Status));
            } finally { ClientX.QueryDnsRequestOverride = null; }
        }

        /// <summary>Caller cancellation must stop admission before another server is attempted.</summary>
        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task QueryDns_CallerCancellationStopsServerFallback(bool streamed, bool allServers) {
            using var cancellation = new CancellationTokenSource();
            int calls = 0;
            try {
                ClientX.QueryDnsRequestOverride = (_, _, _, token) => {
                    calls++;
                    cancellation.Cancel();
                throw new TaskCanceledException("Caller canceled the query.");
                };

                await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                    ExecuteServerTestRequest(ServerTestRequest(allServers), streamed, cancellation.Token));
                Assert.Equal(1, calls);
            } finally { ClientX.QueryDnsRequestOverride = null; }
        }

        /// <summary>A later DNS failure response supersedes an earlier thrown transport error after retries are exhausted.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task QueryDns_FinalRetryResponseReplacesOlderException(bool streamed) {
            int calls = 0;
            try {
                ClientX.QueryDnsRequestOverride = (_, names, type, _) => {
                    if (++calls == 1) throw new TimeoutException("Earlier deadline elapsed.");
                    var response = ServerTestResponse(names[0], type, DnsResponseCode.ServerFailure);
                    response.Error = "Final DNS server failure.";
                    return Task.FromResult(new[] { response });
                };
                var request = ServerTestRequest(allServers: false);
                request.Servers = new[] { request.Servers[0] };
                request.Fallback = false;
                request.RetryCount = 2;

                DnsResponse response = Assert.Single(await ExecuteServerTestRequest(request, streamed));

                Assert.Equal(2, calls);
                Assert.Equal(DnsResponseCode.ServerFailure, response.Status);
                Assert.Equal("Final DNS server failure.", response.Error);
                Assert.Null(response.Exception);
            } finally { ClientX.QueryDnsRequestOverride = null; }
        }

        /// <summary>Single-server execution without a fallback/reporting flag keeps its exception contract.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task QueryDns_SingleServerDeadlineStillThrowsWithoutFallback(bool streamed) {
            try {
                ClientX.QueryDnsRequestOverride = (_, _, _, _) => throw new TimeoutException("Resolver deadline elapsed.");
                var request = ServerTestRequest(allServers: false);
                request.Fallback = false;

                await Assert.ThrowsAsync<TimeoutException>(() => ExecuteServerTestRequest(request, streamed));
            } finally { ClientX.QueryDnsRequestOverride = null; }
        }

        private static ResolveDnsRequest ServerTestRequest(bool allServers) => new() {
            Names = new[] { "example.com" }, RecordTypes = new[] { DnsRecordType.A },
            Servers = new[] { "192.0.2.1", "192.0.2.2" }, RequestFormat = DnsRequestFormat.DnsOverHttps,
            Fallback = !allServers, AllServers = allServers, RetryCount = 1, RetryDelayMs = 0, MaxConcurrency = 1
        };

        private static DnsResponse ServerTestResponse(string name, DnsRecordType type, DnsResponseCode status) => new() {
            Status = status,
            Questions = new[] { new DnsQuestion { Name = name, OriginalName = name, Type = type, RequestFormat = DnsRequestFormat.DnsOverHttps } }
        };

        private static async Task<DnsResponse[]> ExecuteServerTestRequest(ResolveDnsRequest request, bool streamed,
            CancellationToken cancellationToken = default) {
            if (!streamed) return await ClientX.QueryDns(request, cancellationToken);
            var responses = new List<DnsResponse>();
            await foreach (DnsResponse response in ClientX.QueryDnsStreaming(request, cancellationToken: cancellationToken)) responses.Add(response);
            return responses.ToArray();
        }

    }
}
