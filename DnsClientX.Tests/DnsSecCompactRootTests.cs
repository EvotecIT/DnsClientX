using System.Net;
using System.Net.Sockets;

namespace DnsClientX.Tests;

/// <summary>Exercises compact negotiation and alias settlement through actual iterative wire queries.</summary>
public class DnsSecCompactRootTests {
    private const string Missing = "missing.example.com";
    private const string Alias = "alias.example.com";

    /// <summary>The opt-out reaches both the answer query and every trust-material lookup.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IterativeQueriesCarryCompactNegotiation(bool compact) {
        using var fixture = new DnsSecSignedFixture();
        var denial = fixture.Nsec(Missing, "\\000." + Missing, DnsRecordType.NSEC, DnsRecordType.RRSIG, DnsRecordType.NXNAME);
        var observed = new List<(DnsRecordType Type, bool Compact)>();
        var response = await Resolve(fixture, Missing, compact, (_, _) => denial, observed);
        Assert.Equal(DnsSecValidationStatus.Secure, response.DnsSecValidationStatus);
        Assert.Equal(DnsResponseCode.NXDomain, response.EffectiveStatus);
        Assert.Contains(observed, query => query.Type == DnsRecordType.DNSKEY);
        Assert.Contains(observed, query => query.Type == DnsRecordType.DS);
        Assert.All(observed, query => Assert.Equal(compact, query.Compact));
    }

    /// <summary>Both allowed compact header forms settle consistently across alias segments.</summary>
    [Theory]
    [InlineData(DnsResponseCode.NXDomain, DnsResponseCode.NoError)]
    [InlineData(DnsResponseCode.NoError, DnsResponseCode.NXDomain)]
    [InlineData(DnsResponseCode.NXDomain, DnsResponseCode.NXDomain)]
    [InlineData(DnsResponseCode.NoError, DnsResponseCode.NoError)]
    public async Task IterativeAliasesUseAuthenticatedDenialSemantics(DnsResponseCode aliasStatus, DnsResponseCode finalStatus) {
        using var fixture = new DnsSecSignedFixture();
        var denial = fixture.Nsec(Missing, "\\000." + Missing, DnsRecordType.NSEC, DnsRecordType.RRSIG, DnsRecordType.NXNAME);
        var alias = fixture.Signed(Alias, DnsRecordType.CNAME, DnsWireNameCodec.ToCanonicalWire(Missing));
        DnsSecSignedFixture.WithProofs(alias, denial);
        alias.Status = aliasStatus;
        denial.Status = finalStatus;
        var response = await Resolve(fixture, Alias, true, (name, _) => name == Alias ? alias : denial, new());
        Assert.Equal(DnsSecValidationStatus.Secure, response.DnsSecValidationStatus);
        Assert.True(response.DnsSecCompactDenial);
        Assert.Equal(finalStatus, response.Status);
        Assert.Equal(DnsResponseCode.NXDomain, response.EffectiveStatus);
    }

    private static async Task<DnsResponse> Resolve(DnsSecSignedFixture fixture, string name, bool compact,
        Func<string, DnsRecordType, DnsResponse> answer, List<(DnsRecordType Type, bool Compact)> observed) {
        using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        int port = ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var stop = deadline.Token.Register(udp.Dispose);
        Task server = Serve();
        using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverUDP, timeOutMilliseconds: 1000);
        client.EndpointConfiguration.EnableQNameMinimization = false;
        client.EndpointConfiguration.Rfc5011TrustAnchorStorePath = fixture.AnchorPath;
        client.EndpointConfiguration.EdnsOptions = new EdnsOptions { CompactAnswersOk = compact };
        try {
            return await client.ResolveFromRoot(name, DnsRecordType.A, new[] { "127.0.0.1" },
                16, port, requestDnsSec: true, validateDnsSec: true, deadline.Token);
        } finally {
            deadline.Cancel();
            await server;
        }

        async Task Serve() {
            try {
                while (!deadline.IsCancellationRequested) {
                    var query = await udp.ReceiveAsync();
                    var reader = new DnsWireReader(query.Buffer, 12);
                    string owner = DnsWireNameCodec.TrimTrailingRootDot(reader.ReadName());
                    var type = (DnsRecordType)reader.ReadUInt16();
                    reader.ReadUInt16();
                    int questionEnd = reader.Position;
                    observed.Add((type, (query.Buffer[query.Buffer.Length - 4] & 0x40) != 0));
                    var response = type is DnsRecordType.DNSKEY or DnsRecordType.DS
                        ? await fixture.LookupAsync(owner, type, deadline.Token) : answer(owner, type);
                    byte[] bytes = Serialize(query.Buffer, questionEnd, response);
                    await udp.SendAsync(bytes, bytes.Length, query.RemoteEndPoint);
                }
            } catch (Exception ex) when (deadline.IsCancellationRequested
                && ex is ObjectDisposedException or SocketException or OperationCanceledException) { }
        }
    }

    private static byte[] Serialize(byte[] query, int questionEnd, DnsResponse response) {
        var bytes = new List<byte>();
        U16((ushort)((query[0] << 8) | query[1])); U16((ushort)(0x8400 | (ushort)response.Status));
        U16(1); U16((ushort)response.WireAnswers.Length); U16((ushort)response.WireAuthorities.Length); U16(0);
        bytes.AddRange(query.Skip(12).Take(questionEnd - 12));
        foreach (var record in response.WireAnswers.Concat(response.WireAuthorities)) {
            bytes.AddRange(DnsWireNameCodec.ToCanonicalWire(record.Name));
            U16((ushort)record.Type); U16(record.Class);
            bytes.Add((byte)(record.RawTtl >> 24)); bytes.Add((byte)(record.RawTtl >> 16));
            bytes.Add((byte)(record.RawTtl >> 8)); bytes.Add((byte)record.RawTtl);
            U16(record.RdataLength);
            bytes.AddRange(response.WireMessage.Skip(record.RdataOffset).Take(record.RdataLength));
        }
        return bytes.ToArray();
        void U16(ushort value) { bytes.Add((byte)(value >> 8)); bytes.Add((byte)value); }
    }
}
