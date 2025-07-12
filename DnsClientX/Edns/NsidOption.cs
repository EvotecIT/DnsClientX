namespace DnsClientX;

using System;

/// <summary>
/// Implements the NSID option defined in RFC 5001.
/// </summary>
public sealed class NsidOption : EdnsOption {
    /// <summary>
    /// Initializes a new instance of the <see cref="NsidOption"/> class.
    /// </summary>
    /// <param name="data">Optional NSID data. When null an empty request is sent.</param>
    public NsidOption(byte[]? data = null) : base(3) => Data = data ?? Array.Empty<byte>();

    /// <summary>
    /// Gets the data contained in the option.
    /// </summary>
    public byte[] Data { get; }

    /// <inheritdoc/>
    protected override byte[] GetData() => Data;
}
