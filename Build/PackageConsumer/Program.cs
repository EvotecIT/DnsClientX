using DnsClientX;
using DnsClientX.DnsSec.EdDsa;

var response = new DnsResponse {
    Answers = [new DnsAnswer { Name = "package.example", Type = DnsRecordType.A, Class = 1, TTL = 9, DataRaw = "192.0.2.1" }]
};
var record = response.GetTypedAnswers<ARecord>().Single();
if (record.Name != "package.example" || record.TTL != 9 || record.Value.Address.ToString() != "192.0.2.1")
    throw new Exception("Typed metadata contract failed.");
using var client = new ClientX(DnsEndpoint.System);
var verifier = new EdDsaDnsSecSignatureVerifier();
client.EndpointConfiguration.DnsSecSignatureVerifier = verifier;
if (!client.EndpointConfiguration.SupportedDnsSecAlgorithms.Contains(DnsKeyAlgorithm.ED25519)) throw new Exception("Optional verifier failed to load.");
// RFC 8032 section 7.1, first Ed25519 test vector. This invokes the shipped
// cryptographic dependency rather than merely inspecting its algorithm list.
var publicKey = Convert.FromHexString("d75a980182b10ab7d54bfed3c964073a0ee172f3daa62325af021a68f707511a");
var signature = Convert.FromHexString("e5564300c360ac729086e2cc806e828a84877f1eb8e5d974d873e06522490155" +
    "5fb8821590a33bacc61e39701cf9b46bd25bf5f0595bbe24655141438e7a100b");
if (!verifier.Verify(DnsKeyAlgorithm.ED25519, publicKey, [], signature)) throw new Exception("Packed provider rejected the Ed25519 vector.");
signature[0] ^= 1;
if (verifier.Verify(DnsKeyAlgorithm.ED25519, publicKey, [], signature)) throw new Exception("Packed provider accepted an invalid signature.");
new DnsResolveOptions { TypedRecords = true }.Validate();
_ = ClientX.GetResponseCacheStatistics();
_ = DnsQueryExplanation.Create(response);
Console.WriteLine($"Packed core and optional DNSSEC provider loaded on {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}.");
