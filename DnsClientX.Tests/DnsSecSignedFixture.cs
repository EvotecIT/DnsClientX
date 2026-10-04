using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace DnsClientX.Tests {
    // Real RSA signatures and a persisted root anchor exercise the complete trust chain.
    internal sealed class DnsSecSignedFixture : IDisposable {
        private readonly RSA _rsa = RSA.Create();
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "DnsClientX-signed-" + Guid.NewGuid().ToString("N"));
        internal DateTimeOffset Now { get; } = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        internal DnsSecKey Root { get; }
        internal DnsSecKey Zone { get; }
        internal string AnchorPath => Path.Combine(_directory, "anchors.json");
        internal string DsSigner { get; set; } = ".";
        internal uint MaterialTtl { get; set; } = 3600;
        internal int DsLifetime { get; set; } = 3600;
        internal int KeyLifetime { get; set; } = 3600;

        internal DnsSecSignedFixture() {
            _rsa.KeySize = 2048;
            RSAParameters parameters = _rsa.ExportParameters(false);
            byte[] key = new[] { (byte)parameters.Exponent!.Length }.Concat(parameters.Exponent).Concat(parameters.Modulus!).ToArray();
            Root = new DnsSecKey(".", 257, 3, 8, key);
            Zone = new DnsSecKey("example.com", 257, 3, 8, key);
            Directory.CreateDirectory(_directory);
            File.WriteAllText(AnchorPath, DnsClientXJsonSerializer.Serialize(new Rfc5011StateFile {
                LastSuccessfulRefreshUtc = Now.AddDays(-1),
                Keys = new List<Rfc5011StateKey> { new() {
                    Id = Rfc5011Store.KeyIdentity(Root), KeyTag = Root.KeyTag, Flags = Root.Flags,
                    Protocol = Root.Protocol, Algorithm = Root.Algorithm, PublicKey = Convert.ToBase64String(key),
                    State = DnsSecTrustAnchorState.Valid, FirstSeenUtc = Now.AddDays(-60), LastSeenUtc = Now.AddDays(-1)
                } }
            }));
        }

        internal DnsSecValidationEngine Engine(bool currentTime = false,
            DnsResponseCode dnskeyStatus = DnsResponseCode.NoError,
            DnsResponseCode dsStatus = DnsResponseCode.NoError) => new(async (name, type, token) => {
            await Task.Yield();
            token.ThrowIfCancellationRequested();
            if (type == DnsRecordType.DNSKEY) {
                DnsSecKey key = name == "." ? Root : Zone;
                DnsResponse response = Signed(key.Name, type, new byte[] { 1, 1, 3, 8 }.Concat(key.PublicKey).ToArray(),
                    key, ttl: MaterialTtl, lifetime: KeyLifetime);
                response.Status = dnskeyStatus;
                return response;
            }
            if (type == DnsRecordType.DS && DnsWireNameCodec.Canonical(name) == Zone.Name) {
                AssertDigest(Zone, out byte[] digest);
                byte[] data = new[] { (byte)(Zone.KeyTag >> 8), (byte)Zone.KeyTag, Zone.Algorithm, (byte)2 }.Concat(digest).ToArray();
                var signer = new DnsSecKey(DsSigner, Root.Flags, Root.Protocol, Root.Algorithm, Root.PublicKey);
                DnsResponse response = Signed(Zone.Name, type, data, signer, ttl: MaterialTtl, lifetime: DsLifetime);
                response.Status = dsStatus;
                return response;
            }
            throw new InvalidOperationException($"Unexpected fixture lookup: {name} {type}");
        }, currentTime ? null : Now, AnchorPath);

        private static void AssertDigest(DnsSecKey key, out byte[] digest) {
            if (!DnsSecCrypto.TryComputeDsDigest(key.Name, key, 2, out digest)) throw new InvalidOperationException("SHA-256 DS is unavailable.");
        }

        internal DnsResponse Signed(string owner, DnsRecordType type, byte[] rdata, DnsSecKey? key = null,
            byte? labels = null, uint ttl = 3600, uint originalTtl = 3600, uint signatureTtl = 3600,
            int lifetime = 3600, bool authority = false) {
            DnsSecKey signer = key ?? Zone;
            string canonical = DnsWireNameCodec.Canonical(owner);
            byte labelCount = labels ?? (byte)(canonical == "." ? 0 : canonical.TrimEnd('.').Split('.').Length);
            uint expiration = unchecked((uint)Now.AddSeconds(lifetime).ToUnixTimeSeconds());
            uint inception = unchecked((uint)Now.AddMinutes(-1).ToUnixTimeSeconds());
            string data = type == DnsRecordType.CNAME || type == DnsRecordType.DNAME
                ? new DnsWireReader(rdata, 0, rdata.Length).ReadName() : string.Empty;
            var rr = new DnsWireResourceRecord(canonical, type, 1, (int)ttl, ttl, 0, (ushort)rdata.Length, data);
            var signature = new DnsSecSignature(canonical, 1, type, 8, labelCount, originalTtl, expiration, inception,
                signer.KeyTag, signer.Name, Array.Empty<byte>());
            byte[] signed = _rsa.SignData(DnsSecWire.BuildSignedData(rdata, signature, new[] { rr }),
                HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var wire = new List<byte>(rdata);
            int offset = wire.Count;
            U16(wire, (ushort)type); wire.Add(8); wire.Add(labelCount); U32(wire, originalTtl);
            U32(wire, expiration); U32(wire, inception); U16(wire, signer.KeyTag);
            wire.AddRange(DnsWireNameCodec.ToCanonicalWire(signer.Name)); wire.AddRange(signed);
            var sig = new DnsWireResourceRecord(canonical, DnsRecordType.RRSIG, 1, (int)signatureTtl, signatureTtl,
                offset, checked((ushort)(wire.Count - offset)), string.Empty);
            return new DnsResponse {
                Status = DnsResponseCode.NoError, WireMessage = wire.ToArray(),
                WireAnswers = authority ? Array.Empty<DnsWireResourceRecord>() : new[] { rr, sig },
                WireAuthorities = authority ? new[] { rr, sig } : Array.Empty<DnsWireResourceRecord>(),
                Answers = authority ? Array.Empty<DnsAnswer>() : new[] { new DnsAnswer { Name = canonical, Type = type, TTL = (int)ttl, DataRaw = data } }
            };
        }

        internal DnsResponse Nsec(string owner, string next, params DnsRecordType[] types) =>
            Signed(owner, DnsRecordType.NSEC, DnsWireNameCodec.ToCanonicalWire(next).Concat(Bitmap(types)).ToArray(), authority: true);

        internal DnsResponse Nsec3(string name, string next, bool optOut = false, params DnsRecordType[] types) {
            return Nsec3ForZone(name, next, Zone, optOut, types);
        }

        internal DnsResponse RootNsec3(string name, string next, params DnsRecordType[] types) {
            return Nsec3ForZone(name, next, Root, false, types);
        }

        private DnsResponse Nsec3ForZone(string name, string next, DnsSecKey key, bool optOut, DnsRecordType[] types) {
            byte[] hash = Hash(name);
            byte[] nextHash = Hash(next);
            byte[] data = new byte[] { 1, (byte)(optOut ? 1 : 0), 0, 0, 0, 20 }.Concat(nextHash).Concat(Bitmap(types)).ToArray();
            string owner = Base32(hash) + (key.Name == "." ? "." : "." + key.Name);
            return Signed(owner, DnsRecordType.NSEC3, data, key: key, authority: true);
        }

        internal static DnsResponse WithProofs(DnsResponse answer, params DnsResponse[] proofs) {
            var wire = new List<byte>(answer.WireMessage);
            var authorities = new List<DnsWireResourceRecord>();
            foreach (DnsResponse proof in proofs) {
                int offset = wire.Count;
                wire.AddRange(proof.WireMessage);
                authorities.AddRange(proof.WireAuthorities.Select(record => new DnsWireResourceRecord(record.Name,
                    record.Type, record.Class, record.Ttl, record.RawTtl, record.RdataOffset + offset,
                    record.RdataLength, record.Data)));
            }
            answer.WireMessage = wire.ToArray(); answer.WireAuthorities = authorities.ToArray();
            return answer;
        }

        private static byte[] Bitmap(IEnumerable<DnsRecordType> types) {
            var output = new List<byte>();
            foreach (var group in types.Select(type => (ushort)type).GroupBy(type => type / 256).OrderBy(group => group.Key)) {
                byte[] bitmap = new byte[(group.Max() % 256) / 8 + 1];
                foreach (ushort type in group) bitmap[(type % 256) / 8] |= (byte)(1 << (7 - type % 8));
                output.Add((byte)group.Key); output.Add((byte)bitmap.Length); output.AddRange(bitmap);
            }
            return output.ToArray();
        }
        private static byte[] Hash(string name) {
            using SHA1 sha = SHA1.Create();
            return sha.ComputeHash(DnsWireNameCodec.ToCanonicalWire(name));
        }
        private static string Base32(byte[] bytes) {
            const string alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUV";
            var result = new System.Text.StringBuilder(); int buffer = 0, bits = 0;
            foreach (byte b in bytes) {
                buffer = (buffer << 8) | b; bits += 8;
                while (bits >= 5) { bits -= 5; result.Append(alphabet[(buffer >> bits) & 31]); }
            }
            if (bits > 0) result.Append(alphabet[(buffer << (5 - bits)) & 31]);
            return result.ToString();
        }
        private static void U16(List<byte> bytes, ushort n) { bytes.Add((byte)(n >> 8)); bytes.Add((byte)n); }
        private static void U32(List<byte> bytes, uint n) { bytes.Add((byte)(n >> 24)); bytes.Add((byte)(n >> 16)); bytes.Add((byte)(n >> 8)); bytes.Add((byte)n); }
        public void Dispose() { _rsa.Dispose(); Directory.Delete(_directory, recursive: true); }
    }
}
