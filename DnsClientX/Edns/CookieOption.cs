namespace DnsClientX;

using System;

/// <summary>
/// Implements the EDNS cookie option defined in RFC 7873.
/// </summary>
public sealed class CookieOption : EdnsOption {
    /// <summary>
    /// Minimum valid cookie length.
    /// </summary>
    public const int MinCookieLength = 8;

    /// <summary>
    /// Maximum valid cookie length.
    /// </summary>
    public const int MaxCookieLength = 40;

    /// <summary>
    /// Initializes a new instance of the <see cref="CookieOption"/> class.
    /// </summary>
    /// <param name="data">Cookie bytes to include in the option.</param>
    public CookieOption(byte[] data) : base(10) {
        if (data == null) {
            throw new ArgumentNullException(nameof(data));
        }

        if (!IsValidLength(data.Length)) {
            throw new ArgumentException($"Cookie length must be between {MinCookieLength} and {MaxCookieLength} bytes.", nameof(data));
        }

        Data = new byte[data.Length];
        Array.Copy(data, Data, data.Length);
    }

    /// <summary>
    /// Gets the cookie bytes contained in the option.
    /// </summary>
    public byte[] Data { get; }

    internal static bool IsValidLength(int length) => length >= MinCookieLength && length <= MaxCookieLength;

    /// <inheritdoc/>
    protected override byte[] GetData() => Data;
}
