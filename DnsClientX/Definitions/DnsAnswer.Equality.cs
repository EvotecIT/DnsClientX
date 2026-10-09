using System;

namespace DnsClientX;

public partial struct DnsAnswer {
    /// <summary>Compares record values without treating evidence buffer allocation as record identity.</summary>
    /// <param name="other">Record to compare.</param>
    public bool Equals(DnsAnswer other) => Type == other.Type && TTL == other.TTL && Class == other.Class
        && string.Equals(_name, other._name, StringComparison.Ordinal)
        && string.Equals(OriginalName, other.OriginalName, StringComparison.Ordinal)
        && string.Equals(DataRaw, other.DataRaw, StringComparison.Ordinal);

    /// <summary>Determines whether another object contains the same record value.</summary>
    /// <param name="obj">Object to compare.</param>
    public override bool Equals(object? obj) => obj is DnsAnswer answer && Equals(answer);

    /// <summary>Computes a hash from the record fields used by value equality.</summary>
    public override int GetHashCode() {
        unchecked {
            int hash = 17;
            hash = hash * 31 + (int)Type;
            hash = hash * 31 + TTL;
            hash = hash * 31 + Class.GetHashCode();
            hash = hash * 31 + (_name == null ? 0 : StringComparer.Ordinal.GetHashCode(_name));
            hash = hash * 31 + (OriginalName == null ? 0 : StringComparer.Ordinal.GetHashCode(OriginalName));
            return hash * 31 + (DataRaw == null ? 0 : StringComparer.Ordinal.GetHashCode(DataRaw));
        }
    }
}
