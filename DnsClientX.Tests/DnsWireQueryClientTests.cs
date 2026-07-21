using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests;

/// <summary>Contract tests for caller-constructed diagnostic DNS messages.</summary>
public class DnsWireQueryClientTests {
    /// <summary>Custom query flags and classes flow through the shared validated UDP transport.</summary>
    [Fact]
    public async Task QueryUdpAsync_PreservesCustomQueryAndReportsWireSizes() {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        int port = ((IPEndPoint)server.Client.LocalEndPoint!).Port;
        Task<byte[]> serverTask = Task.Run(async () => {
            UdpReceiveResult request = await server.ReceiveAsync();
            byte[] response = TestUtilities.CreateResponseFromQuery(request.Buffer, flags: 0x8000);
            await server.SendAsync(response, response.Length, request.RemoteEndPoint);
            return request.Buffer;
        });
        var message = new DnsMessage("version.bind", DnsRecordType.TXT, new DnsMessageOptions(
            RecursionDesired: false,
            QueryClass: 3));

        DnsWireQueryResult result = await DnsWireQueryClient.QueryUdpAsync(
            "127.0.0.1", port, message, 2000, useTcpFallback: false);
        byte[] requestBytes = await serverTask;

        Assert.Equal(0, requestBytes[2] & 0x01);
        Assert.Equal(3, (requestBytes[requestBytes.Length - 2] << 8) | requestBytes[requestBytes.Length - 1]);
        Assert.Equal(requestBytes.Length, result.QueryMessage.Length);
        Assert.Equal(result.ResponseMessage.Length, result.Response.WireMessageLength);
    }

    /// <summary>Caller cancellation is not relabeled as a transport timeout.</summary>
    [Fact]
    public async Task QueryUdpAsync_PropagatesCallerCancellation() {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        int port = ((IPEndPoint)server.Client.LocalEndPoint!).Port;
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var message = new DnsMessage("example.com", DnsRecordType.A, new DnsMessageOptions());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => DnsWireQueryClient.QueryUdpAsync(
            "127.0.0.1", port, message, 5000, useTcpFallback: false, cancellationToken: cancellation.Token));
    }

    /// <summary>The public lightweight parser uses the same safe name reader as full response parsing.</summary>
    [Fact]
    public void TryParseQuestion_ReturnsQuestionMetadata() {
        var message = new DnsMessage("version.bind", DnsRecordType.TXT, new DnsMessageOptions(QueryClass: 3));
        byte[] response = TestUtilities.CreateResponseFromQuery(message.SerializeDnsWireFormat());

        Assert.True(DnsWireMessageParser.TryParseQuestion(response, 0, out DnsWireQuestionInfo question));
        Assert.Equal("version.bind", question.Name);
        Assert.Equal((ushort)DnsRecordType.TXT, question.Type);
        Assert.Equal((ushort)3, question.Class);
    }
}
