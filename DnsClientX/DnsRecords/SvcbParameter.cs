using System;

namespace DnsClientX;

/// <summary>Preserves one SVCB/HTTPS parameter and its exact wire-format value.</summary>
public sealed class SvcbParameter {
    private readonly byte[] _value;

    /// <summary>Gets the numeric parameter key, including keys unknown to this library.</summary>
    public ushort Key { get; }

    /// <summary>Gets the recognized parameter name, or its numeric <c>keyNNNN</c> name.</summary>
    public string Name => DnsSvcbCodec.KeyName(Key);

    /// <summary>Gets an independent copy of the wire-format parameter value.</summary>
    public byte[] Value => (byte[])_value.Clone();

    internal byte[] Bytes => _value;

    /// <summary>Creates a parameter from its numeric key and wire-format value.</summary>
    /// <param name="key">Numeric SvcParamKey.</param>
    /// <param name="value">Wire-format value; the constructor retains an independent copy.</param>
    public SvcbParameter(ushort key, byte[] value) {
        if (value == null) throw new ArgumentNullException(nameof(value));
        if (value.Length > ushort.MaxValue) throw new ArgumentException("SVCB parameter exceeds the wire length limit.", nameof(value));
        Key = key;
        _value = (byte[])value.Clone();
        DnsSvcbCodec.ValidateParameter(this);
    }
}
