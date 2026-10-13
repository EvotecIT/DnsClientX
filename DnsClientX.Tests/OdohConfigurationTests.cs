using System;
using System.IO;
using DnsClientX.Odoh;

namespace DnsClientX.Tests;

/// <summary>Configuration format, preference and invalid-key contracts.</summary>
public class OdohConfigurationTests {
    /// <summary>Unknown versions and suites are skipped while list ordering and explicit expiry are preserved.</summary>
    [Fact]
    public void IgnoresUnknownVersionsAndSuitesAndSelectsFirstSupported() {
        byte[] good = OdohTestVectors.Hex(OdohTestVectors.Config);
        var unknownVersion = (byte[])good.Clone();
        unknownVersion[3] = 2;
        var unknownSuite = (byte[])good.Clone();
        unknownSuite[9] = 2;
        byte[] combined = JoinConfigurations(unknownVersion, unknownSuite, good);
        DateTimeOffset expiry = DateTimeOffset.UtcNow.AddHours(2);
        OdohTargetConfiguration configuration = OdohTargetConfiguration.Parse(combined, expiry);
        Assert.Equal(expiry, configuration.ExpiresAt);
        Assert.Equal(OdohTestVectors.Hex(OdohTestVectors.KeyId), configuration.KeyId);
        byte[] publicKey = configuration.PublicKey;
        publicKey[0] ^= 1;
        Assert.NotEqual(publicKey, configuration.PublicKey);
        byte[] keyId = configuration.KeyId;
        keyId[0] ^= 1;
        Assert.NotEqual(keyId, configuration.KeyId);
        Assert.Throws<NotSupportedException>(() => OdohTargetConfiguration.Parse(unknownVersion, expiry));
        Assert.Throws<NotSupportedException>(() => OdohTargetConfiguration.Parse(unknownSuite, expiry));
    }

    /// <summary>Malformed framing cannot hide truncated or extra configuration contents, even after a supported key.</summary>
    [Fact]
    public void RejectsMalformedListsAndInvalidX25519Keys() {
        byte[] good = OdohTestVectors.Hex(OdohTestVectors.Config);
        for (int length = 0; length < good.Length; length++) {
            var truncated = new byte[length];
            Array.Copy(good, truncated, length);
            Assert.Throws<InvalidDataException>(() => OdohTargetConfiguration.Parse(truncated, DateTimeOffset.UtcNow));
        }
        var malformed = (byte[])good.Clone();
        malformed[5]++;
        Assert.Throws<InvalidDataException>(() => OdohTargetConfiguration.Parse(malformed, DateTimeOffset.UtcNow));
        var zeroKey = (byte[])good.Clone();
        Array.Clear(zeroKey, 14, 32);
        Assert.Throws<InvalidDataException>(() => OdohTargetConfiguration.Parse(zeroKey, DateTimeOffset.UtcNow));
        var smallOrderKey = (byte[])zeroKey.Clone();
        smallOrderKey[14] = 1;
        Assert.Throws<InvalidDataException>(() => OdohTargetConfiguration.Parse(smallOrderKey, DateTimeOffset.UtcNow));
        var invalidLength = (byte[])good.Clone();
        invalidLength[13] = 31;
        Assert.Throws<InvalidDataException>(() => OdohTargetConfiguration.Parse(invalidLength, DateTimeOffset.UtcNow));
    }

    private static byte[] JoinConfigurations(params byte[][] lists) {
        int length = 0;
        foreach (byte[] list in lists) {
            length += list.Length - 2;
        }
        var result = new byte[length + 2];
        result[0] = (byte)(length >> 8);
        result[1] = (byte)length;
        int offset = 2;
        foreach (byte[] list in lists) {
            Array.Copy(list, 2, result, offset, list.Length - 2);
            offset += list.Length - 2;
        }
        return result;
    }
}
