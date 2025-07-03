using System;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    internal static class DnsCrypt {
        private const string CertMagic = "DNSC";
        private const string ResolverMagic = "r6fnvWj8";

        internal static async Task<DnsCryptCertificate> Handshake(string dnsServer, int port, string providerName, string providerPublicKey, bool debug, Configuration config, CancellationToken token) {
            string certName = $"2.dnscrypt-cert.{providerName}";
            DnsResponse certResponse = await DnsWireResolveUdp.ResolveWireFormatUdp(dnsServer, port, certName, DnsRecordType.TXT, false, false, debug, config, token);
            if (certResponse.Answers == null) {
                throw new DnsClientException("No certificate records returned.");
            }
            foreach (var answer in certResponse.Answers) {
                if (string.IsNullOrWhiteSpace(answer.DataRaw)) continue;
                try {
                    byte[] data = Convert.FromBase64String(answer.DataRaw.Trim('"'));
                    var cert = ParseCertificate(data, providerPublicKey);
                    if (cert != null) return cert;
                } catch {
                    continue;
                }
            }
            throw new DnsClientException("Valid DNSCrypt certificate not found.");
        }

        private static DnsCryptCertificate ParseCertificate(byte[] data, string providerPublicKey) {
            if (data.Length < 124) return null;
            if (!data.AsSpan(0, 4).SequenceEqual(Encoding.ASCII.GetBytes(CertMagic))) return null;
            ushort esVersion = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(4, 2));
            if (esVersion != 2) return null;
            // skip minor version
            ReadOnlySpan<byte> signature = data.AsSpan(8, 64);
            ReadOnlySpan<byte> resolverPk = data.AsSpan(72, 32);
            ReadOnlySpan<byte> clientMagic = data.AsSpan(104, 8);
            ReadOnlySpan<byte> serial = data.AsSpan(112, 4);
            ReadOnlySpan<byte> tsStart = data.AsSpan(116, 4);
            ReadOnlySpan<byte> tsEnd = data.AsSpan(120, 4);
            ReadOnlySpan<byte> extensions = data.Length > 124 ? data.AsSpan(124) : ReadOnlySpan<byte>.Empty;

            Span<byte> signed = new byte[resolverPk.Length + clientMagic.Length + serial.Length + tsStart.Length + tsEnd.Length + extensions.Length];
            int offset = 0;
            resolverPk.CopyTo(signed);
            offset += resolverPk.Length;
            clientMagic.CopyTo(signed[offset..]);
            offset += clientMagic.Length;
            serial.CopyTo(signed[offset..]);
            offset += serial.Length;
            tsStart.CopyTo(signed[offset..]);
            offset += tsStart.Length;
            tsEnd.CopyTo(signed[offset..]);
            offset += tsEnd.Length;
            extensions.CopyTo(signed[offset..]);

            byte[] publicKey = Convert.FromBase64String(providerPublicKey);
            if (Sodium.crypto_sign_ed25519_verify_detached(signature.ToArray(), signed.ToArray(), (ulong)signed.Length, publicKey) != 0) {
                return null;
            }

            uint now = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            uint start = BinaryPrimitives.ReadUInt32BigEndian(tsStart);
            uint end = BinaryPrimitives.ReadUInt32BigEndian(tsEnd);
            if (now < start || now > end) return null;

            return new DnsCryptCertificate {
                ResolverPublicKey = resolverPk.ToArray(),
                ClientMagic = clientMagic.ToArray()
            };
        }

        internal static void GenerateKeyPair(out byte[] pk, out byte[] sk) {
            pk = new byte[32];
            sk = new byte[32];
            Sodium.crypto_box_curve25519xchacha20poly1305_keypair(pk, sk);
        }

        internal static byte[] ComputeSharedSecret(byte[] clientPk, byte[] clientSk, byte[] resolverPk) {
            byte[] rx = new byte[32];
            byte[] tx = new byte[32];
            if (Sodium.crypto_kx_client_session_keys(rx, tx, clientPk, clientSk, resolverPk) != 0) {
                throw new DnsClientException("Failed to derive shared secret.");
            }
            return rx; // use receive key
        }

        internal static byte[] Encrypt(byte[] key, byte[] nonce, byte[] plain) {
            byte[] cipher = new byte[plain.Length + 16];
            Sodium.crypto_aead_xchacha20poly1305_ietf_encrypt(cipher, out _, plain, (ulong)plain.Length, IntPtr.Zero, 0, IntPtr.Zero, nonce, key);
            return cipher;
        }

        internal static byte[] Decrypt(byte[] key, byte[] nonce, byte[] cipher) {
            byte[] plain = new byte[cipher.Length - 16];
            if (Sodium.crypto_aead_xchacha20poly1305_ietf_decrypt(plain, out _, IntPtr.Zero, cipher, (ulong)cipher.Length, IntPtr.Zero, 0, nonce, key) != 0) {
                throw new DnsClientException("Decryption failed");
            }
            return plain;
        }

        internal static byte[] PadQuery(byte[] query) {
            int min = 256;
            int paddedLength = ((Math.Max(query.Length + 1, min) + 63) / 64) * 64;
            byte[] padded = new byte[paddedLength];
            Buffer.BlockCopy(query, 0, padded, 0, query.Length);
            padded[query.Length] = 0x80;
            return padded;
        }

        internal static byte[] RemovePadding(byte[] data) {
            int idx = Array.LastIndexOf(data, (byte)0x80);
            if (idx >= 0) {
                byte[] result = new byte[idx];
                Buffer.BlockCopy(data, 0, result, 0, idx);
                return result;
            }
            return data;
        }

        internal static async Task<byte[]> SendUdp(byte[] payload, string server, int port, int timeout, CancellationToken token) {
            using UdpClient client = new();
            client.Client.SendTimeout = timeout;
            client.Client.ReceiveTimeout = timeout;
            await client.SendAsync(payload, payload.Length, server, port);
            var result = await client.ReceiveAsync(token);
            return result.Buffer;
        }

        internal static async Task<DnsResponse> QueryUdp(string serverForHandshake, int portForHandshake, string serverForQuery, int portForQuery, string providerName, string providerPk, string name, DnsRecordType type, bool debug, Configuration config, CancellationToken token) {
            var cert = await Handshake(serverForHandshake, portForHandshake, providerName, providerPk, debug, config, token);
            GenerateKeyPair(out var clientPk, out var clientSk);
            byte[] shared = ComputeSharedSecret(clientPk, clientSk, cert.ResolverPublicKey);

            var query = new DnsMessage(name, type, false, config.EnableEdns, config.UdpBufferSize);
            byte[] queryBytes = query.SerializeDnsWireFormat();
            byte[] padded = PadQuery(queryBytes);

            byte[] clientNonce = new byte[12];
            RandomNumberGenerator.Fill(clientNonce);
            byte[] nonce = new byte[24];
            Buffer.BlockCopy(clientNonce, 0, nonce, 0, 12);
            byte[] cipher = Encrypt(shared, nonce, padded);

            byte[] packet = new byte[cert.ClientMagic.Length + clientPk.Length + clientNonce.Length + cipher.Length];
            Buffer.BlockCopy(cert.ClientMagic, 0, packet, 0, 8);
            Buffer.BlockCopy(clientPk, 0, packet, 8, 32);
            Buffer.BlockCopy(clientNonce, 0, packet, 40, 12);
            Buffer.BlockCopy(cipher, 0, packet, 52, cipher.Length);

            byte[] response = await SendUdp(packet, serverForQuery, portForQuery, config.TimeOut, token);
            if (response.Length < 40) throw new DnsClientException("Invalid response size");
            if (!response.AsSpan(0,8).SequenceEqual(Encoding.ASCII.GetBytes(ResolverMagic))) {
                throw new DnsClientException("Invalid resolver magic");
            }
            byte[] nonceResp = new byte[24];
            Buffer.BlockCopy(response, 8, nonceResp, 0, 24);
            Buffer.BlockCopy(clientNonce, 0, nonceResp, 0, 12);
            byte[] decrypted = Decrypt(shared, nonceResp, response.AsSpan(32).ToArray());
            byte[] dnsData = RemovePadding(decrypted);
            var dnsResponse = await DnsWire.DeserializeDnsWireFormat(null, debug, dnsData);
            dnsResponse.AddServerDetails(config);
            return dnsResponse;
        }
    }
}
