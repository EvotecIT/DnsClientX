using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace DnsClientX.Tests;
/// <summary>Independent, uncompressed wire records for discovery protocol fixtures.</summary>
internal static class DnsDiscoveryTestWire {
    internal readonly struct Record {
        internal readonly string Name;
        internal readonly DnsRecordType Type;
        internal readonly byte[] Data;
        internal readonly uint Ttl;
        internal readonly bool Flush;
        internal Record(string name, DnsRecordType type, byte[] data, uint ttl = 120, bool flush = false) {
            Name = name;
            Type = type;
            Data = data;
            Ttl = ttl;
            Flush = flush;
        }
    }

    internal static byte[] Response(byte[]? query, params Record[] records) {
        using var result = new MemoryStream();
        byte[] header = query == null ? new byte[12] : TestUtilities.CreateResponseFromQuery(query);
        header[2] = query == null ? (byte)0x84 : (byte)0x81;
        header[3] = query == null ? (byte)0 : (byte)0x80;
        header[6] = (byte)(records.Length >> 8);
        header[7] = (byte)records.Length;
        result.Write(header, 0, header.Length);
        foreach (Record record in records) {
            byte[] name = Name(record.Name);
            result.Write(name, 0, name.Length);
            UInt16(result, (ushort)record.Type);
            UInt16(result, record.Flush ? (ushort)0x8001 : (ushort)1);
            result.WriteByte((byte)(record.Ttl >> 24));
            result.WriteByte((byte)(record.Ttl >> 16));
            result.WriteByte((byte)(record.Ttl >> 8));
            result.WriteByte((byte)record.Ttl);
            UInt16(result, checked((ushort)record.Data.Length));
            result.Write(record.Data, 0, record.Data.Length);
        }

        return result.ToArray();
    }

    internal static byte[] Name(string name) {
        using var result = new MemoryStream();
        foreach (string label in name.TrimEnd('.').Split('.')) {
            if (label.Length == 0) {
                continue;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(label);
            result.WriteByte(checked((byte)bytes.Length));
            result.Write(bytes, 0, bytes.Length);
        }

        result.WriteByte(0);
        return result.ToArray();
    }

    internal static Record Svcb(string owner, ushort priority, string target, string? alpn = null, int? port = null, string? path = null, IPAddress? hint = null, uint ttl = 120) {
        using var result = new MemoryStream();
        UInt16(result, priority);
        byte[] name = Name(target);
        result.Write(name, 0, name.Length);
        if (alpn != null) {
            byte[] protocol = Encoding.ASCII.GetBytes(alpn);
            Param(result, 1, new[] { checked((byte)protocol.Length) }.Concat(protocol).ToArray());
        }

        if (port.HasValue) {
            Param(result, 3, new[] { (byte)(port.Value >> 8), (byte)port.Value });
        }

        if (hint != null && hint.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) {
            Param(result, 4, hint.GetAddressBytes());
        }

        if (hint != null && hint.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6) {
            Param(result, 6, hint.GetAddressBytes());
        }

        if (path != null) {
            Param(result, 7, Encoding.UTF8.GetBytes(path));
        }

        return new Record(owner, DnsRecordType.SVCB, result.ToArray(), ttl);
    }

    internal static Record Ptr(string type, string instance, uint ttl = 120) => new(type, DnsRecordType.PTR, Name(instance), ttl);
    internal static Record Srv(string instance, string target, ushort port, uint ttl = 120, bool flush = true) {
        using var data = new MemoryStream();
        UInt16(data, 0);
        UInt16(data, 0);
        UInt16(data, port);
        byte[] name = Name(target);
        data.Write(name, 0, name.Length);
        return new Record(instance, DnsRecordType.SRV, data.ToArray(), ttl, flush);
    }

    internal static Record Txt(string instance, string text, uint ttl = 120, bool flush = true) {
        byte[] data = Encoding.UTF8.GetBytes(text);
        return new Record(instance, DnsRecordType.TXT, new[] { checked((byte)data.Length) }.Concat(data).ToArray(), ttl, flush);
    }

    internal static Record Address(string target, string address, uint ttl = 120) {
        IPAddress ip = IPAddress.Parse(address);
        return new Record(target, ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? DnsRecordType.A : DnsRecordType.AAAA, ip.GetAddressBytes(), ttl, true);
    }

    private static void Param(Stream target, ushort key, byte[] value) {
        UInt16(target, key);
        UInt16(target, checked((ushort)value.Length));
        target.Write(value, 0, value.Length);
    }

    private static void UInt16(Stream stream, ushort value) {
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }
}

/// <summary>Creates a private CA and native-server leaf from generated software keys without using certificate stores.</summary>
/// <remarks>Disposal releases the leaf's temporary Windows key container or macOS keychain; Linux keys remain ephemeral.</remarks>
internal sealed class DnsDiscoveryCertificateFixture : IDisposable {
    private readonly RSA _rootKey = RSA.Create();
    private readonly RSA _leafKey = RSA.Create();
    internal X509Certificate2 Root { get; }
    internal X509Certificate2 Leaf { get; }

    internal DnsDiscoveryCertificateFixture(IPAddress? sanAddress, byte[]? rawSan = null) {
        _rootKey.KeySize = 2048;
        _leafKey.KeySize = 2048;
        var root = new CertificateRequest("CN=DNS discovery fixture CA", _rootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        root.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        root.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        Root = root.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        var request = new CertificateRequest("CN=127.0.0.1", _leafKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        var usage = new OidCollection {
            new Oid("1.3.6.1.5.5.7.3.1")
        };
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(usage, false));
        if (rawSan != null) {
            request.CertificateExtensions.Add(new X509Extension("2.5.29.17", rawSan, false));
        }
        else {
            var san = new SubjectAlternativeNameBuilder();
            san.AddDnsName("resolver.example");
            if (sanAddress != null) {
                san.AddIpAddress(sanAddress);
            }

            request.CertificateExtensions.Add(san.Build());
        }

        using var publicLeaf = request.Create(Root, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(30), new byte[] { 1, 2, 3, 4, 5 });
        using var keyedLeaf = publicLeaf.CopyWithPrivateKey(_leafKey);
        Leaf = TestUtilities.ImportForServer(keyedLeaf);
    }

    public void Dispose() {
        Leaf.Dispose();
        Root.Dispose();
        _leafKey.Dispose();
        _rootKey.Dispose();
    }
}
