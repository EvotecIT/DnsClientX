using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DnsClientX;
using DnsClientX.DnsCrypt;
using DnsClientX.Odoh;

// Package-only public API proof: no project references, production internals or real network endpoints.
byte[] query = Hex("123401000001000000000000076578616d706c6503636f6d0000010001");
byte[] encoded = Hex("002c000100280020000100010020ae3bf1cd87c2d2ed25af4a1a239eed04a990f00e7403e4c8065927de010fd17a");
using var handler = new RefusingRelay();
using var http = new HttpClient(handler);
using var odoh = new OdohClient(new OdohClientOptions(new Uri("https://relay.example/proxy"), new Uri("https://target.example/dns-query")) {
    InitialConfiguration = OdohTargetConfiguration.Parse(encoded, DateTimeOffset.UtcNow.AddMinutes(10))
}, http);
if (!DnsTransportCapabilities.GetConfiguredCapability(odoh.CreateConfiguration()).Supported) {
    throw new Exception("ODoH registration missing");
}
try { await odoh.QueryAsync(query); throw new Exception("An HTTP failure was accepted as DNS"); }
catch (HttpRequestException) { }
if (handler.Calls != 1) {
    throw new Exception("ODoH retried an application query or used another transport");
}
using var canceled = new CancellationTokenSource(); canceled.Cancel();
using var dnscrypt = new DnsCryptClient(new DnsCryptOptions(new IPEndPoint(IPAddress.Loopback, 1), "2.dnscrypt-cert.example", new byte[32]));
if (string.IsNullOrEmpty(DnsCryptClient.NativeLibraryVersion)) {
    throw new Exception("libsodium did not load");
}
if (!DnsTransportCapabilities.GetConfiguredCapability(dnscrypt.CreateConfiguration()).Supported) {
    throw new Exception("DNSCrypt registration missing");
}
try { await dnscrypt.QueryExchangeAsync(query, canceled.Token); throw new Exception("Pre-canceled DNSCrypt query was dispatched"); }
catch (OperationCanceledException) { }
using var core = ResolverExecutionClientFactory.CreateConfiguredClient(dnscrypt.CreateConfiguration());
var batch = new DnsUpdateBatch("example.test", new[] { DnsUpdateOperation.DeleteName("old.example.test") }, new[] { DnsUpdatePrerequisite.NameAbsent("new.example.test") });
if (batch.Operations.Count != 1 || batch.Prerequisites.Count != 1) {
    throw new Exception("Core candidate API missing");
}
Console.WriteLine("PASS package-only core/ODoH/DNSCrypt public APIs; BC encrypts relay request; HTTP failure fails closed; native libsodium " + DnsCryptClient.NativeLibraryVersion + " loads; pre-cancel avoids network.");

static byte[] Hex(string value) {
    var bytes = new byte[value.Length / 2];
    for (int i = 0; i < bytes.Length; i++) {
        bytes[i] = Convert.ToByte(value.Substring(i * 2, 2), 16);
    }
    return bytes;
}

sealed class RefusingRelay : HttpMessageHandler {
    internal int Calls;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) {
        Calls++;
        if (request.RequestUri?.Host != "relay.example" || request.Method != HttpMethod.Post
            || request.Content?.Headers.ContentType?.MediaType != "application/oblivious-dns-message") {
            throw new InvalidDataException("Invalid relay envelope");
        }
        byte[] body = await request.Content!.ReadAsByteArrayAsync();
        if (body.Length <= 32 || body[0] != 1 || Contains(body, new byte[] { 7, 101, 120, 97, 109, 112, 108, 101, 3, 99, 111, 109, 0 })) {
            throw new InvalidDataException("DNS name was not encrypted");
        }
        return new HttpResponseMessage(HttpStatusCode.Unauthorized) { RequestMessage = request };
    }
    private static bool Contains(byte[] message, byte[] value) {
        for (int i = 0; i <= message.Length - value.Length; i++) {
            if (value.Where((b, index) => message[i + index] != b).Any()) {
                continue;
            }
            return true;
        }
        return false;
    }
}
