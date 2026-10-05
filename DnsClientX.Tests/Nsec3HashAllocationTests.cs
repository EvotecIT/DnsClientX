using System;
using System.Reflection;
namespace DnsClientX.Tests;
/// <summary>Checks accepted NSEC3 hashes against independent wire-format vectors.</summary>
public sealed class Nsec3HashAllocationTests {
    // Expected bytes were generated with Python hashlib over literal canonical DNS wire labels.
    /// <summary>Salt, iteration count, root names and canonical case preserve the defined digest.</summary>
    [Theory]
    [InlineData("www.example.com", 0, "1234abcd", "3cba81f861617add2166a646a8489a18f06a11a3")]
    [InlineData("www.example.com", 1, "1234abcd", "c90071bc68204f3758b3b98cb8252a743d0f8e94")]
    [InlineData("www.example.com", 500, "1234abcd", "0e467b44f919ea3dd72417f494580afa65346359")]
    [InlineData(".", 0, "", "5ba93c9db0cff93f52b521d7420e43f6eda2784f")]
    [InlineData(".", 1, "", "7ab8dc8456c25f132551f157c77a1888ef918fac")]
    [InlineData(".", 500, "", "a3e65ff4be64fa1db5c6dfa0799882c95043b624")]
    [InlineData("WWW.Example.COM", 0, "", "b49edbb7a3bbde3c34a3c1fb55063b7bd259d3d4")]
    [InlineData("WWW.Example.COM", 1, "", "057eede37564f0a156722ee54063b298eb0c4c75")]
    [InlineData("WWW.Example.COM", 500, "", "f67091573896df5ffb6b41a825bb427059769284")]
    public void AcceptedHashMatchesIndependentProducer(string name, int iterations, string saltHex, string expected) {
        var hash = typeof(ClientX).Assembly.GetType("DnsClientX.DnsSecProof", true)!
            .GetMethod("HashName", BindingFlags.Static | BindingFlags.NonPublic)!;
        byte[] salt = new byte[saltHex.Length/2];
        for (int i=0;i<salt.Length;i++) { salt[i]=Convert.ToByte(saltHex.Substring(i*2,2),16); }
        byte[] result = (byte[])hash.Invoke(null,new object[] { name,(ushort)iterations,salt })!;
        Assert.Equal(expected, BitConverter.ToString(result).Replace("-","").ToLowerInvariant());
    }
}
