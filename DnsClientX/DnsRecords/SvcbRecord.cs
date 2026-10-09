using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;

namespace DnsClientX;

/// <summary>Represents the shared service-binding data of an SVCB or HTTPS record.</summary>
/// <remarks>Defined by RFC 9460. Unknown parameters retain their numeric keys and exact values.</remarks>
public sealed class SvcbRecord {
    /// <summary>Gets the service priority; zero identifies AliasMode.</summary>
    public ushort Priority { get; }

    /// <summary>Gets the normalized target name, or <c>.</c> for the owner-name target.</summary>
    public string Target { get; }

    /// <summary>Gets whether this record is in AliasMode, where service parameters are ignored.</summary>
    public bool IsAliasMode => Priority == 0;

    /// <summary>Gets parameters in numeric key order, including unknown parameters.</summary>
    public IReadOnlyDictionary<ushort, SvcbParameter> Parameters { get; }

    /// <summary>Gets the keys explicitly required by the mandatory parameter.</summary>
    public IReadOnlyList<ushort> MandatoryKeys => Array.AsReadOnly(Parameters.TryGetValue(0, out var parameter)
        ? DnsSvcbCodec.ReadKeys(parameter.Bytes) : Array.Empty<ushort>());

    /// <summary>Gets ALPN identifiers as octet-preserving strings.</summary>
    public IReadOnlyList<string> Alpn => Array.AsReadOnly(Parameters.TryGetValue(1, out var parameter)
        ? DnsSvcbCodec.ReadAlpn(parameter.Bytes) : Array.Empty<string>());

    /// <summary>Gets whether the no-default-alpn parameter is present.</summary>
    public bool NoDefaultAlpn => Parameters.ContainsKey(2);

    /// <summary>Gets the advertised port, or null when no port parameter is present.</summary>
    public ushort? Port => Parameters.TryGetValue(3, out var parameter)
        ? (ushort)((parameter.Bytes[0] << 8) | parameter.Bytes[1]) : null;

    /// <summary>Gets independent copies of the advertised IPv4 address hints.</summary>
    public IReadOnlyList<IPAddress> Ipv4Hints => ReadAddresses(4, 4);

    /// <summary>Gets independent copies of the advertised IPv6 address hints.</summary>
    public IReadOnlyList<IPAddress> Ipv6Hints => ReadAddresses(6, 16);

    /// <summary>Gets an independent copy of the ECH configuration, or null when absent.</summary>
    public byte[]? EchConfiguration => Parameters.TryGetValue(5, out var parameter) ? parameter.Value : null;

    internal string OriginalTarget { get; }

    /// <summary>Creates immutable service-binding data from a target and wire-format parameters.</summary>
    /// <param name="priority">Service priority, or zero for AliasMode.</param>
    /// <param name="target">DNS target name, including the root-label sentinel.</param>
    /// <param name="parameters">Parameters with unique numeric keys.</param>
    public SvcbRecord(ushort priority, string target, IEnumerable<SvcbParameter>? parameters = null) {
        if (string.IsNullOrWhiteSpace(target)) throw new ArgumentException("SVCB target name is required.", nameof(target));
        DnsWireNameCodec.ToCanonicalWire(target);
        Priority = priority;
        OriginalTarget = target;
        Target = DnsRecordDataPresentation.NormalizeName(target);
        var values = new SortedDictionary<ushort, SvcbParameter>();
        foreach (var parameter in parameters ?? Array.Empty<SvcbParameter>()) {
            if (parameter == null || values.ContainsKey(parameter.Key))
                throw new ArgumentException("SVCB parameter keys must be unique.", nameof(parameters));
            values.Add(parameter.Key, parameter);
        }
        Parameters = new ReadOnlyDictionary<ushort, SvcbParameter>(values);
        DnsSvcbCodec.ValidateRecord(this);
    }

    /// <summary>Returns the canonical provider-independent record presentation.</summary>
    public override string ToString() => DnsSvcbCodec.Format(this);

    private IReadOnlyList<IPAddress> ReadAddresses(ushort key, int width) {
        if (!Parameters.TryGetValue(key, out var parameter)) return Array.AsReadOnly(Array.Empty<IPAddress>());
        var addresses = new IPAddress[parameter.Bytes.Length / width];
        for (int index = 0; index < addresses.Length; index++) {
            var bytes = new byte[width];
            Buffer.BlockCopy(parameter.Bytes, index * width, bytes, 0, width);
            addresses[index] = new IPAddress(bytes);
        }
        return Array.AsReadOnly(addresses);
    }
}
