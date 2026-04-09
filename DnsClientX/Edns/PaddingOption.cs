namespace DnsClientX;

using System;

/// <summary>
/// Implements the EDNS padding option defined in RFC 7830.
/// </summary>
public sealed class PaddingOption : EdnsOption {
    /// <summary>
    /// Initializes a new instance of the <see cref="PaddingOption"/> class.
    /// </summary>
    /// <param name="length">Padding length in bytes.</param>
    public PaddingOption(int length) : base(12) {
        if (length < 0 || length > ushort.MaxValue) {
            throw new ArgumentOutOfRangeException(nameof(length), $"Padding length must be between 0 and {ushort.MaxValue}.");
        }

        Length = length;
    }

    /// <summary>
    /// Gets the padding length in bytes.
    /// </summary>
    public int Length { get; }

    /// <inheritdoc/>
    protected override byte[] GetData() => new byte[Length];
}
