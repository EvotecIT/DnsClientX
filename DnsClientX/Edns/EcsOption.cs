namespace DnsClientX;

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

/// <summary>
/// Implements the EDNS Client Subnet option (ECS) as defined in RFC 7871.
/// </summary>
/// <remarks>
/// This option allows resolvers to tailor responses based on the network of the client.
/// </remarks>
public sealed class EcsOption : EdnsOption {
    /// <summary>
    /// Gets the subnet in CIDR notation.
    /// </summary>
    public string Subnet { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EcsOption"/> class.
    /// </summary>
    /// <param name="subnet">Subnet in CIDR notation.</param>
    public EcsOption(string subnet) : base(8) => Subnet = subnet;

    /// <inheritdoc/>
    protected override byte[] GetData() {
        string[] parts = Subnet.Split('/');
        if (!IPAddress.TryParse(parts[0], out var ip)) {
            throw new ArgumentException("Invalid subnet", nameof(Subnet));
        }
        int prefixLength = parts.Length > 1 ? int.Parse(parts[1]) : (ip.AddressFamily == AddressFamily.InterNetwork ? 32 : 128);

        ushort family = ip.AddressFamily == AddressFamily.InterNetwork ? (ushort)1 : (ushort)2;
        byte[] addressBytes = ip.GetAddressBytes();
        int addressBits = prefixLength;
        int addressBytesLen = (addressBits + 7) / 8;
        if (addressBytesLen > addressBytes.Length) addressBytesLen = addressBytes.Length;
        byte[] truncated = new byte[addressBytesLen];
        Array.Copy(addressBytes, truncated, addressBytesLen);
        int unusedBits = addressBytesLen * 8 - addressBits;
        if (unusedBits > 0 && addressBytesLen > 0) {
            truncated[addressBytesLen - 1] &= (byte)(0xFF << unusedBits);
        }

        using var ms = new MemoryStream();
        void WriteUInt16(ushort value) => ms.Write(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)value)), 0, 2);

        WriteUInt16(family);
        ms.WriteByte((byte)prefixLength);
        ms.WriteByte(0); // scope prefix length
        ms.Write(truncated, 0, truncated.Length);
        return ms.ToArray();
    }
}
