using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math.EC.Rfc7748;

namespace DnsClientX.Tests;

/// <summary>Protocol-shaped HTTP boundary that decrypts real provider requests and replies with DNS wire answers.
/// This receiver uses its own RFC schedule; exact interoperability is separately anchored to the Go fixture.</summary>
internal sealed class OdohTestTarget : HttpMessageHandler {
    internal readonly ConcurrentBag<byte[]> EncryptedQueries = new ConcurrentBag<byte[]>();
    internal int ConfigRequests;
    internal int QueryRequests;
    internal bool Disposed;
    internal TimeSpan? CacheMaxAge = TimeSpan.FromHours(1);
    internal TimeSpan? Age;
    internal bool NoStore;
    internal bool RejectNextQuery;
    internal Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? Override;
    internal Action<byte[]>? MutateResponse;
    internal Action<byte[]>? ObserveQuery;
    internal Uri? LastRelayUri;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        if (Override != null) {
            return await Override(request, cancellationToken);
        }
        var response = new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request };
        if (request.Method == HttpMethod.Get) {
            Interlocked.Increment(ref ConfigRequests);
            response.Content = new ByteArrayContent(OdohTestVectors.Hex(OdohTestVectors.Config));
            response.Headers.CacheControl = new CacheControlHeaderValue { MaxAge = CacheMaxAge, NoStore = NoStore };
            response.Headers.Age = Age;
            return response;
        }
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("application/oblivious-dns-message", request.Content!.Headers.ContentType!.MediaType);
        Assert.True(request.Headers.CacheControl!.NoStore);
        LastRelayUri = request.RequestUri;
        Interlocked.Increment(ref QueryRequests);
        byte[] bytes = await request.Content.ReadAsByteArrayAsync();
        EncryptedQueries.Add(bytes);
        if (RejectNextQuery) {
            RejectNextQuery = false;
            response.StatusCode = HttpStatusCode.Unauthorized;
            return response;
        }
        byte[] reply = Reply(bytes, ObserveQuery);
        MutateResponse?.Invoke(reply);
        response.Content = new ByteArrayContent(reply);
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/oblivious-dns-message");
        return response;
    }

    internal static byte[] Reply(byte[] message, Action<byte[]>? observeQuery = null) {
        Assert.Equal(1, message[0]);
        int offset = 1;
        byte[] keyId = ReadVector(message, ref offset);
        Assert.Equal(OdohTestVectors.Hex(OdohTestVectors.KeyId), keyId);
        byte[] encrypted = ReadVector(message, ref offset);
        Assert.Equal(message.Length, offset);
        var enc = new byte[32];
        Array.Copy(encrypted, enc, 32);
        var ciphertext = new byte[encrypted.Length - 32];
        Array.Copy(encrypted, 32, ciphertext, 0, ciphertext.Length);
        var dh = new byte[32];
        Assert.True(X25519.CalculateAgreement(OdohTestVectors.Hex(OdohTestVectors.TargetPrivateKey), 0, enc, 0, dh, 0));
        byte[] kemSuite = { 0x4b, 0x45, 0x4d, 0, 0x20 };
        byte[] hpkeSuite = { 0x48, 0x50, 0x4b, 0x45, 0, 0x20, 0, 1, 0, 1 };
        byte[] publicKey = OdohTestVectors.Hex(OdohTestVectors.Config).SkipBytes(14);
        byte[] eae = LabeledExtract(kemSuite, Array.Empty<byte>(), "eae_prk", dh);
        byte[] shared = LabeledExpand(kemSuite, eae, "shared_secret", Join(enc, publicKey), 32);
        byte[] schedule = Join(new byte[] { 0 }, LabeledExtract(hpkeSuite, Array.Empty<byte>(), "psk_id_hash", Array.Empty<byte>()),
            LabeledExtract(hpkeSuite, Array.Empty<byte>(), "info_hash", Encoding.ASCII.GetBytes("odoh query")));
        byte[] secret = LabeledExtract(hpkeSuite, shared, "secret", Array.Empty<byte>());
        byte[] key = LabeledExpand(hpkeSuite, secret, "key", schedule, 16);
        byte[] nonce = LabeledExpand(hpkeSuite, secret, "base_nonce", schedule, 12);
        byte[] exporter = LabeledExpand(hpkeSuite, secret, "exp", schedule, 32);
        byte[] plaintext = Aead(false, key, nonce, Join(new byte[] { 1 }, Vector(keyId)), ciphertext);
        offset = 0;
        byte[] query = ReadVector(plaintext, ref offset);
        observeQuery?.Invoke((byte[])query.Clone());
        byte[] padding = ReadVector(plaintext, ref offset);
        Assert.All(padding, value => Assert.Equal(0, value));
        byte[] response = CreateDnsAnswer(query);
        byte[] responseSecret = LabeledExpand(hpkeSuite, exporter, "sec", Encoding.ASCII.GetBytes("odoh response"), 16);
        var responseNonce = new byte[16];
        using (RandomNumberGenerator random = RandomNumberGenerator.Create()) {
            random.GetBytes(responseNonce);
        }
        byte[] prk = Extract(Join(plaintext, Vector(responseNonce)), responseSecret);
        byte[] responseKey = Expand(prk, Encoding.ASCII.GetBytes("odoh key"), 16);
        byte[] responseIv = Expand(prk, Encoding.ASCII.GetBytes("odoh nonce"), 12);
        byte[] responsePlaintext = Join(Vector(response), Vector(new byte[7]));
        byte[] encryptedResponse = Aead(true, responseKey, responseIv, Join(new byte[] { 2 }, Vector(responseNonce)), responsePlaintext);
        return Join(new byte[] { 2 }, Vector(responseNonce), Vector(encryptedResponse));
    }

    private static byte[] CreateDnsAnswer(byte[] query) {
        int end = 12;
        while (query[end] != 0) {
            end += query[end] + 1;
        }
        end += 5;
        var answer = new byte[end + 16];
        Array.Copy(query, answer, end);
        answer[2] = 0x81;
        answer[3] = 0x80;
        answer[6] = 0;
        answer[7] = 1;
        answer[8] = answer[9] = answer[10] = answer[11] = 0;
        byte[] record = { 0xc0, 0x0c, 0, 1, 0, 1, 0, 0, 0, 60, 0, 4, 1, 2, 3, 4 };
        Array.Copy(record, 0, answer, end, record.Length);
        return answer;
    }

    private static byte[] LabeledExtract(byte[] suite, byte[] salt, string label, byte[] ikm) {
        return Extract(salt, Join(Encoding.ASCII.GetBytes("HPKE-v1"), suite, Encoding.ASCII.GetBytes(label), ikm));
    }
    private static byte[] LabeledExpand(byte[] suite, byte[] prk, string label, byte[] info, int length) {
        return Expand(prk, Join(new[] { (byte)(length >> 8), (byte)length }, Encoding.ASCII.GetBytes("HPKE-v1"), suite, Encoding.ASCII.GetBytes(label), info), length);
    }
    private static byte[] Extract(byte[] salt, byte[] ikm) {
        using var hmac = new HMACSHA256(salt.Length == 0 ? new byte[32] : salt);
        return hmac.ComputeHash(ikm);
    }
    private static byte[] Expand(byte[] prk, byte[] info, int length) {
        // All receiver fixture expansions fit into one SHA-256 block.
        Assert.InRange(length, 0, 32);
        using var hmac = new HMACSHA256(prk);
        byte[] hash = hmac.ComputeHash(Join(info, new byte[] { 1 }));
        var result = new byte[length];
        Array.Copy(hash, result, length);
        return result;
    }
    private static byte[] Aead(bool encrypt, byte[] key, byte[] nonce, byte[] aad, byte[] input) {
        var cipher = new GcmBlockCipher(new AesEngine());
        cipher.Init(encrypt, new AeadParameters(new KeyParameter(key), 128, nonce, aad));
        var result = new byte[cipher.GetOutputSize(input.Length)];
        int count = cipher.ProcessBytes(input, 0, input.Length, result, 0);
        count += cipher.DoFinal(result, count);
        Assert.Equal(result.Length, count);
        return result;
    }
    private static byte[] ReadVector(byte[] bytes, ref int offset) {
        int length = (bytes[offset] << 8) | bytes[offset + 1];
        offset += 2;
        var result = new byte[length];
        Array.Copy(bytes, offset, result, 0, length);
        offset += length;
        return result;
    }
    private static byte[] Vector(byte[] bytes) {
        return Join(new[] { (byte)(bytes.Length >> 8), (byte)bytes.Length }, bytes);
    }
    private static byte[] Join(params byte[][] arrays) {
        using var stream = new MemoryStream();
        foreach (byte[] array in arrays) {
            stream.Write(array, 0, array.Length);
        }
        return stream.ToArray();
    }
    protected override void Dispose(bool disposing) {
        Disposed = true;
        base.Dispose(disposing);
    }
}

internal static class OdohTestBytes {
    internal static byte[] SkipBytes(this byte[] bytes, int count) {
        var result = new byte[bytes.Length - count];
        Array.Copy(bytes, count, result, 0, result.Length);
        return result;
    }
}
