using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests;

/// <summary>Protects the public workflow contract across typed results, options and request streaming.</summary>
[Collection("NoParallel")]
public class DnsQueryWorkflowTests {
    /// <summary>Filtering payload types retains the matching record's metadata, including unknown records.</summary>
    [Fact]
    public void TypedMetadataFollowsItsRecordRatherThanFilteredArrayPositions() {
        var response = new DnsResponse {
            Answers = new[] {
                new DnsAnswer { Name = "alias.example", Type = DnsRecordType.CNAME, TTL = 13, DataRaw = "target.example" },
                new DnsAnswer { Name = "target.example", Type = DnsRecordType.A, TTL = 7, Class = 1, DataRaw = "192.0.2.1" },
                new DnsAnswer { Name = "bad.example", Type = DnsRecordType.A, TTL = 5, DataRaw = "not-an-address" }
            }
        };
        var address = Assert.Single(response.GetTypedAnswers<ARecord>());
        Assert.Equal("target.example", address.Name);
        Assert.Equal(7, address.TTL);
        Assert.Equal((ushort)1, address.Class);
        Assert.Equal("192.0.2.1", address.Value.Address.ToString());
        Assert.Single(response.GetTypedAnswers<UnknownRecord>());
        Assert.Equal(3, response.GetTypedAnswers<object>().Length);
    }

    /// <summary>Options streaming snapshots behavior and keeps bounded admission until consumers request more.</summary>
    [Fact]
    public async Task OptionsStreamingSnapshotsTypedProjectionAndPreservesInputOrder() {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverUDP, enableCache: false);
        client.EndpointConfiguration.Port = ((IPEndPoint)server.Client.LocalEndPoint!).Port;
        client.EndpointConfiguration.MaxConcurrency = 2;
        var peer = Task.Run(async () => {
            for (int index = 0; index < 2; index++) {
                var receive = server.ReceiveAsync();
                Assert.Same(receive, await Task.WhenAny(receive, Task.Delay(5000)));
                var query = await receive;
                int questionEnd = 12;
                while (query.Buffer[questionEnd] != 0) questionEnd += query.Buffer[questionEnd] + 1;
                questionEnd += 5;
                byte[] answer = query.Buffer.Take(questionEnd).Concat(new byte[] {
                    0xc0, 0x0c, 0, 1, 0, 1, 0, 0, 0, 15, 0, 4, 192, 0, 2, 1
                }).ToArray();
                answer[2] = 0x81; answer[3] = 0x80; answer[6] = 0; answer[7] = 1;
                answer[8] = answer[9] = answer[10] = answer[11] = 0;
                await server.SendAsync(answer, answer.Length, query.RemoteEndPoint);
            }
        });
        var options = new DnsResolveOptions { TypedRecords = true, RetryOnTransient = false };
        var stream = client.ResolveStreaming(new[] { "one.example", "two.example" }, new[] { DnsRecordType.A }, options);
        options.TypedRecords = false;
        var results = await CollectAsync(stream);
        await peer;
        Assert.Equal(new[] { "one.example", "two.example" }, results.Select(r => r.Answers[0].Name));
        Assert.All(results, result => Assert.IsType<ARecord>(Assert.Single(result.TypedAnswers!)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Resolve("example", DnsRecordType.A,
            new DnsResolveOptions { MaxRetries = -1 }));
    }

    /// <summary>Request streaming retains source and DNSSEC/typed policy for every name/type combination.</summary>
    [Fact]
    public async Task RequestStreamingPreservesPolicyAndTypeMajorOrder() {
        try {
            ClientX.QueryDnsRequestOverride = (request, names, type, token) => {
                Assert.True(request.TypedRecords);
                Assert.True(request.ValidateDnsSec);
                Assert.Equal(DnsEndpoint.Cloudflare, Assert.Single(request.DnsProviders));
                return Task.FromResult(names.Select(name => new DnsResponse {
                    Questions = new[] { new DnsQuestion { Name = name, Type = type } }
                }).ToArray());
            };
            var results = await CollectAsync(ClientX.QueryDnsStreaming(new ResolveDnsRequest {
                Names = new[] { "one.example", "two.example" }, RecordTypes = new[] { DnsRecordType.A, DnsRecordType.AAAA },
                DnsProviders = new[] { DnsEndpoint.Cloudflare }, TypedRecords = true, ValidateDnsSec = true, MaxConcurrency = 2
            }));
            Assert.Equal(new[] { DnsRecordType.A, DnsRecordType.A, DnsRecordType.AAAA, DnsRecordType.AAAA },
                results.Select(r => r.Questions[0].Type));
            Assert.Equal(new[] { "one.example", "two.example", "one.example", "two.example" },
                results.Select(r => r.Questions[0].Name));
        } finally { ClientX.QueryDnsRequestOverride = null; }
    }

    private static async Task<DnsResponse[]> CollectAsync(IAsyncEnumerable<DnsResponse> stream) {
        var results = new List<DnsResponse>();
        await foreach (var response in stream) results.Add(response);
        return results.ToArray();
    }
}
