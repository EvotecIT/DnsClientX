namespace DnsClientX;

using System;
using System.IO;
using System.Net;

/// <summary>
/// Base class for EDNS options.
/// </summary>
/// <remarks>
/// All specific EDNS option implementations derive from this type.
/// </remarks>
public abstract class EdnsOption {
    /// <summary>
    /// Gets the option code as defined in RFC 6891.
    /// </summary>
    public ushort Code { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EdnsOption"/> class.
    /// </summary>
    /// <param name="code">EDNS option code.</param>
    protected EdnsOption(ushort code) => Code = code;

    /// <summary>
    /// Serializes the option to a byte array.
    /// </summary>
    /// <returns>Byte array containing the serialized option.</returns>
    internal byte[] ToByteArray() {
        byte[] data = GetData();
        using var ms = new MemoryStream();
        void WriteUInt16(ushort value) => ms.Write(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)value)), 0, 2);
        WriteUInt16(Code);
        WriteUInt16((ushort)data.Length);
        if (data.Length > 0) {
            ms.Write(data, 0, data.Length);
        }
        return ms.ToArray();
    }

    /// <summary>
    /// Retrieves the option data portion.
    /// </summary>
    /// <returns>Raw option data without the code and length.</returns>
    protected abstract byte[] GetData();
}
