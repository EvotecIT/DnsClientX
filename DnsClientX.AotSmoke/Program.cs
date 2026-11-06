using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using DnsClientX;

using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
int port = ((IPEndPoint)server.Client.LocalEndPoint!).Port;
Task serve = ServeAsync(server, cancellation.Token);
try {
    using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverUDP,
        timeOutMilliseconds: 2000, enableCache: true);
    client.EndpointConfiguration.Port = port;
    DnsResponse response = await client.Resolve("example.com", DnsRecordType.A,
        retryOnTransient: false, typedRecords: true, cancellationToken: cancellation.Token);
    await serve;
    if (response.Status != DnsResponseCode.NoError || response.Answers.Length != 1 ||
        response.Answers[0].DataRaw != "192.0.2.1" || response.TypedAnswers?.Length != 1) {
        throw new InvalidOperationException("The native UDP consumer lost its answer or typed record.");
    }
    DnsResponse cached = await client.Resolve("example.com", DnsRecordType.A,
        retryOnTransient: false, typedRecords: true, cancellationToken: cancellation.Token);
    if (!cached.ServedFromCache || cached.Answers.Single().DataRaw != "192.0.2.1") {
        throw new InvalidOperationException("The native DNS cache lost its answer.");
    }

    string output = DnsResponseTextFormatter.BuildOutputLines(response,
        new DnsResponsePresentationOptions { Mode = DnsResponsePresentationMode.Json },
        TimeSpan.Zero, static duration => duration.ToString()).Single();
    using JsonDocument json = JsonDocument.Parse(output);
    if (json.RootElement.GetProperty("Answer")[0].GetProperty("data").GetString() != "192.0.2.1") {
        throw new InvalidOperationException("The native JSON formatter lost the original answer.");
    }
    DnsResponse restored = DnsClientXJsonSerializer.Deserialize(output, SmokeJsonContext.Default.DnsResponse)
        ?? throw new InvalidOperationException("The generated DNS JSON contract returned null.");
    if (restored.Answers.Single().DataRaw != "192.0.2.1") {
        throw new InvalidOperationException("The generated DNS JSON contract lost the answer.");
    }
    if (!DnsTransportCapabilities.Supports(DnsRequestFormat.DnsOverUDP) ||
        DnsTransportCapabilities.GetCapabilityReport().Length == 0) {
        throw new InvalidOperationException("The native transport capability catalog is incomplete.");
    }
    VerifyPersistence(port);
    ProviderSmoke.Run();
    Console.WriteLine("DnsClientX NativeAOT UDP resolution, typed records, cache, JSON and persistence passed.");
} finally {
    cancellation.Cancel();
    try {
        await serve;
    } catch (OperationCanceledException) when (cancellation.IsCancellationRequested) {
    }
}

static async Task ServeAsync(UdpClient server, CancellationToken cancellationToken) {
    UdpReceiveResult request = await server.ReceiveAsync(cancellationToken);
    byte[] response = Convert.FromHexString(
        "000081800001000100000000076578616d706c6503636f6d0000010001c00c000100010000003c0004c0000201");
    response[0] = request.Buffer[0];
    response[1] = request.Buffer[1];
    await server.SendAsync(response, request.RemoteEndPoint, cancellationToken);
}

static void VerifyPersistence(int port) {
    string directory = Path.Combine(Path.GetTempPath(), "dnsclientx-aot-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try {
        string profilePath = Path.Combine(directory, "profile.json");
        ResolverHealthProfileStore.Save(profilePath, new ResolverHealthProfile {
            Name = "Native AOT",
            ResolverEndpoints = new[] { $"udp@127.0.0.1:{port}" },
            Names = new[] { "example.com" },
            ProbePolicy = new ResolverProbePolicy { MinSuccessCount = 1 },
            BenchmarkPolicy = new ResolverBenchmarkPolicy { MinSuccessfulCandidates = 1 }
        });
        ResolverHealthProfile profile = ResolverHealthProfileStore.Load(profilePath);
        if (profile.Name != "Native AOT" || profile.RecordTypes.Single() != DnsRecordType.A ||
            profile.ResolverEndpoints.Single() != $"udp@127.0.0.1:{port}") {
            throw new InvalidOperationException("The native resolver profile lost persisted values.");
        }
        string scorePath = Path.Combine(directory, "score.json");
        ResolverScoreStore.Save(scorePath, new ResolverScoreSnapshot {
            Summary = new ResolverScoreSummary { Mode = ResolverScoreMode.Benchmark },
            Results = new[] { new ResolverScoreEntry { Target = "loopback", SuccessCount = 1 } }
        });
        ResolverScoreSnapshot score = ResolverScoreStore.Load(scorePath);
        if (score.Summary.Mode != ResolverScoreMode.Benchmark || score.Results.Single().SuccessCount != 1) {
            throw new InvalidOperationException("The native resolver score lost persisted values.");
        }
    } finally {
        Directory.Delete(directory, recursive: true);
    }
}

[JsonSourceGenerationOptions(UseStringEnumConverter = true)]
[JsonSerializable(typeof(DnsResponse))]
internal partial class SmokeJsonContext : JsonSerializerContext { }
