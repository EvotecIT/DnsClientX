using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Xunit;

namespace DnsClientX.Tests {
    public class DnssecChainValidatorTests {
        private static byte[] DomainToWireFormat(string domain) {
            if (string.IsNullOrEmpty(domain) || domain == ".") return new byte[] { 0 };
            string[] labels = domain.TrimEnd('.').Split('.');
            var data = new List<byte>();
            foreach (string label in labels) {
                byte[] bytes = System.Text.Encoding.ASCII.GetBytes(label.ToLowerInvariant());
                data.Add((byte)bytes.Length);
                data.AddRange(bytes);
            }
            data.Add(0);
            return data.ToArray();
        }

        private static ushort ComputeKeyTag(ushort flags, byte protocol, DnsKeyAlgorithm algorithm, byte[] publicKey) {
            byte[] rdata = new byte[4 + publicKey.Length];
            BinaryPrimitives.WriteUInt16BigEndian(rdata, flags);
            rdata[2] = protocol;
            rdata[3] = (byte)algorithm;
            Buffer.BlockCopy(publicKey, 0, rdata, 4, publicKey.Length);
            uint acc = 0;
            for (int i = 0; i < rdata.Length; i++) {
                acc += (i & 1) == 0 ? (uint)rdata[i] << 8 : rdata[i];
            }
            acc += acc >> 16;
            return (ushort)(acc & 0xFFFF);
        }

        private static string ComputeDigest(string name, ushort flags, byte protocol, DnsKeyAlgorithm algorithm, byte[] publicKey) {
            byte[] owner = DomainToWireFormat(name);
            byte[] rdata = new byte[4 + publicKey.Length];
            BinaryPrimitives.WriteUInt16BigEndian(rdata, flags);
            rdata[2] = protocol;
            rdata[3] = (byte)algorithm;
            Buffer.BlockCopy(publicKey, 0, rdata, 4, publicKey.Length);
            byte[] message = new byte[owner.Length + rdata.Length];
            Buffer.BlockCopy(owner, 0, message, 0, owner.Length);
            Buffer.BlockCopy(rdata, 0, message, owner.Length, rdata.Length);
            using SHA256 sha256 = SHA256.Create();
            byte[] digestBytes = sha256.ComputeHash(message);
            return BitConverter.ToString(digestBytes).Replace("-", string.Empty).ToUpperInvariant();
        }

        private static byte[] BuildSignedData(string name, int ttl, DateTime exp, DateTime inc, ushort keyTag, byte[] publicKey) {
            Type validator = typeof(DnsSecValidator);
            Type dnsKeyRec = validator.GetNestedType("DnsKeyRecord", System.Reflection.BindingFlags.NonPublic)!;
            Type rrsigRec = validator.GetNestedType("RrsigRecord", System.Reflection.BindingFlags.NonPublic)!;
            object dnsKey = Activator.CreateInstance(dnsKeyRec, name, (ushort)257, (byte)3, DnsKeyAlgorithm.RSASHA256, publicKey)!;
            var list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(dnsKeyRec))!;
            list.Add(dnsKey);
            object rrsig = Activator.CreateInstance(rrsigRec, DnsRecordType.DNSKEY, DnsKeyAlgorithm.RSASHA256, (byte)name.TrimEnd('.').Split('.').Length, ttl, exp, inc, keyTag, name, Array.Empty<byte>())!;
            var method = validator.GetMethod("BuildDnskeySignedData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            return (byte[])method.Invoke(null, new object[] { rrsig, list })!;
        }


        [Fact]
        public void ValidateChain_Succeeds() {
            using RSA rsa = RSA.Create(1024);
            RSAParameters p = rsa.ExportParameters(true);
            byte[] pub = BuildPublicKey(p);
            string pubB64 = Convert.ToBase64String(pub);
            const string name = "example.com.";
            const ushort flags = 257;
            const byte protocol = 3;
            const DnsKeyAlgorithm alg = DnsKeyAlgorithm.RSASHA256;
            var dnskey = new DnsAnswer { Name = name, Type = DnsRecordType.DNSKEY, TTL = 3600, DataRaw = $"{flags} {protocol} {(int)alg} {pubB64}" };
            ushort tag = ComputeKeyTag(flags, protocol, alg, pub);
            string digest = ComputeDigest(name, flags, protocol, alg, pub);
            var ds = new DnsAnswer { Name = name, Type = DnsRecordType.DS, TTL = 3600, DataRaw = $"{tag} {(int)alg} 2 {digest}" };
            DateTime inception = DateTime.UtcNow.AddMinutes(-1);
            DateTime expiration = DateTime.UtcNow.AddHours(1);
            byte[] data = BuildSignedData(name, 3600, expiration, inception, tag, pub);
            byte[] sig = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            string sigB64 = Convert.ToBase64String(sig);
            var rrsig = new DnsAnswer { Name = name, Type = DnsRecordType.RRSIG, TTL = 3600, DataRaw = $"DNSKEY {(int)alg} 2 3600 {(uint)(expiration - new DateTime(1970,1,1)).TotalSeconds} {(uint)(inception - new DateTime(1970,1,1)).TotalSeconds} {tag} {name} {sigB64}" };
            var response = new DnsResponse { Answers = new[] { dnskey, ds, rrsig } };
            Assert.True(DnsSecValidator.ValidateChain(response, out string msg));
            Assert.Equal(string.Empty, msg);
        }

        [Fact]
        public void ValidateChain_InvalidSignature() {
            using RSA rsa = RSA.Create(1024);
            RSAParameters p = rsa.ExportParameters(true);
            byte[] pub = BuildPublicKey(p);
            string pubB64 = Convert.ToBase64String(pub);
            const string name = "example.com.";
            const ushort flags = 257;
            const byte protocol = 3;
            var dnskey = new DnsAnswer { Name = name, Type = DnsRecordType.DNSKEY, TTL = 3600, DataRaw = $"{flags} {protocol} 8 {pubB64}" };
            ushort tag = ComputeKeyTag(flags, protocol, DnsKeyAlgorithm.RSASHA256, pub);
            string digest = ComputeDigest(name, flags, protocol, DnsKeyAlgorithm.RSASHA256, pub);
            var ds = new DnsAnswer { Name = name, Type = DnsRecordType.DS, TTL = 3600, DataRaw = $"{tag} 8 2 {digest}" };
            DateTime inception = DateTime.UtcNow.AddMinutes(-1);
            DateTime expiration = DateTime.UtcNow.AddHours(1);
            byte[] data = BuildSignedData(name, 3600, expiration, inception, tag, pub);
            byte[] sig = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            sig[0] ^= 0xFF; // corrupt
            string sigB64 = Convert.ToBase64String(sig);
            var rrsig = new DnsAnswer { Name = name, Type = DnsRecordType.RRSIG, TTL = 3600, DataRaw = $"DNSKEY 8 2 3600 {(uint)(expiration - new DateTime(1970,1,1)).TotalSeconds} {(uint)(inception - new DateTime(1970,1,1)).TotalSeconds} {tag} {name} {sigB64}" };
            var response = new DnsResponse { Answers = new[] { dnskey, ds, rrsig } };
            Assert.False(DnsSecValidator.ValidateChain(response, out string msg));
            Assert.Contains("Invalid RRSIG", msg);
        }

        [Fact]
        public void ValidateChain_MissingRecords() {
            var response = new DnsResponse {
                Answers = new[] {
                    new DnsAnswer { Name = "example.com.", Type = DnsRecordType.DS, TTL = 3600, DataRaw = "1 8 2 ABCD" }
                }
            };
            Assert.False(DnsSecValidator.ValidateChain(response, out string msg));
            Assert.Contains("Missing DNSKEY or RRSIG", msg);
        }

        [Fact]
        public void ValidateChain_NoMatchingDnsKey() {
            using RSA rsa = RSA.Create(1024);
            RSAParameters p = rsa.ExportParameters(true);
            byte[] pub = BuildPublicKey(p);
            string pubB64 = Convert.ToBase64String(pub);
            const string name = "example.com.";
            const ushort flags = 257;
            const byte protocol = 3;
            var dnskey = new DnsAnswer { Name = name, Type = DnsRecordType.DNSKEY, TTL = 3600, DataRaw = $"{flags} {protocol} 8 {pubB64}" };
            ushort tag = ComputeKeyTag(flags, protocol, DnsKeyAlgorithm.RSASHA256, pub);
            string digest = ComputeDigest(name, flags, protocol, DnsKeyAlgorithm.RSASHA256, pub);
            var ds = new DnsAnswer { Name = name, Type = DnsRecordType.DS, TTL = 3600, DataRaw = $"{tag + 1} 8 2 {digest}" };
            DateTime inception = DateTime.UtcNow.AddMinutes(-1);
            DateTime expiration = DateTime.UtcNow.AddHours(1);
            byte[] data = BuildSignedData(name, 3600, expiration, inception, tag, pub);
            byte[] sig = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            string sigB64 = Convert.ToBase64String(sig);
            var rrsig = new DnsAnswer { Name = name, Type = DnsRecordType.RRSIG, TTL = 3600, DataRaw = $"DNSKEY 8 2 3600 {(uint)(expiration - new DateTime(1970,1,1)).TotalSeconds} {(uint)(inception - new DateTime(1970,1,1)).TotalSeconds} {tag} {name} {sigB64}" };
            var response = new DnsResponse { Answers = new[] { dnskey, ds, rrsig } };
            Assert.False(DnsSecValidator.ValidateChain(response, out string msg));
            Assert.Contains("No DNSKEY", msg);
        }

        [Fact]
        public void ValidateChain_DigestMismatch() {
            using RSA rsa = RSA.Create(1024);
            RSAParameters p = rsa.ExportParameters(true);
            byte[] pub = BuildPublicKey(p);
            string pubB64 = Convert.ToBase64String(pub);
            const string name = "example.com.";
            const ushort flags = 257;
            const byte protocol = 3;
            var dnskey = new DnsAnswer { Name = name, Type = DnsRecordType.DNSKEY, TTL = 3600, DataRaw = $"{flags} {protocol} 8 {pubB64}" };
            ushort tag = ComputeKeyTag(flags, protocol, DnsKeyAlgorithm.RSASHA256, pub);
            var ds = new DnsAnswer { Name = name, Type = DnsRecordType.DS, TTL = 3600, DataRaw = $"{tag} 8 2 DEAD" };
            DateTime inception = DateTime.UtcNow.AddMinutes(-1);
            DateTime expiration = DateTime.UtcNow.AddHours(1);
            byte[] data = BuildSignedData(name, 3600, expiration, inception, tag, pub);
            byte[] sig = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            string sigB64 = Convert.ToBase64String(sig);
            var rrsig = new DnsAnswer { Name = name, Type = DnsRecordType.RRSIG, TTL = 3600, DataRaw = $"DNSKEY 8 2 3600 {(uint)(expiration - new DateTime(1970,1,1)).TotalSeconds} {(uint)(inception - new DateTime(1970,1,1)).TotalSeconds} {tag} {name} {sigB64}" };
            var response = new DnsResponse { Answers = new[] { dnskey, ds, rrsig } };
            Assert.False(DnsSecValidator.ValidateChain(response, out string msg));
            Assert.Contains("Digest mismatch", msg);
        }

        private static byte[] BuildPublicKey(RSAParameters p) {
            byte[] exponent = p.Exponent!;
            byte[] modulus = p.Modulus!;
            var key = new byte[(exponent.Length > 255 ? 3 : 1) + exponent.Length + modulus.Length];
            int index = 0;
            if (exponent.Length > 255) {
                key[index++] = 0;
                key[index++] = (byte)(exponent.Length >> 8);
                key[index++] = (byte)exponent.Length;
            } else {
                key[index++] = (byte)exponent.Length;
            }
            Buffer.BlockCopy(exponent, 0, key, index, exponent.Length);
            index += exponent.Length;
            Buffer.BlockCopy(modulus, 0, key, index, modulus.Length);
            return key;
        }
    }
}
